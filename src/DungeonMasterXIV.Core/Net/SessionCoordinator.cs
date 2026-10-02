using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>The entry point for session networking: hosting, joining, admission, and the per-frame tick.</summary>
public sealed class SessionCoordinator
{
    private readonly RelayLink _link;

    public SessionCoordinator(
        ISessionTransport transport,
        Func<string> relayAddress,
        TimeSpan window,
        ISessionTransportLog log,
        SessionCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(capabilities);

        _parts = new SessionWiring(transport, relayAddress, window, log, capabilities, () => _stream.LastSequence);

        _log = log;
        _link = _parts.Link;
        _resolveRelink = _parts.ResolveRelink;
        _admissions = _parts.Admissions;
        _handshake = _parts.Handshake;
        _roster = _parts.Roster;
        _resources = _parts.Resources;
        _interruption = _parts.Interruption;
        _joiner = _parts.Joiner;
        _hosting = _parts.Hosting;
        Membership = _parts.Membership;
    }

    private readonly Func<string?, RelinkClaim> _resolveRelink;
    private readonly ISessionTransportLog _log;
    private readonly AdmissionControl _admissions;
    private readonly SessionWiring _parts;
    private readonly OutboundHandshake _handshake;
    private readonly RosterBroadcast _roster;
    private readonly SessionResources _resources;
    private readonly SessionInterruption _interruption;
    private readonly JoinRequester _joiner;
    private readonly HostRunner _hosting;
    private readonly ReceivedRoster _received = new();
    private readonly ReceivedStream _stream = new();
    private readonly PhaseTimeouts _timeouts = new();
    private readonly ReconnectSchedule _reconnect = new();
    private static readonly TimeSpan ResumeRetryInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _sinceResumeSent;

    public IReadOnlyList<RosterEntry> Roster => _received.Entries;

    public IReadOnlyList<StreamLine> Received => _stream.Lines;

    public MemberContentReceipts MemberContent => _resources.MemberContent;
    public IReadOnlyList<StreamEntry> Recorded => _resources.Recording.Entries;

    public HostSession Host => _parts.Host;

    public JoinAttempt Join => _parts.Join;

    public SessionKeyExchange? HostKeys => _hosting.Keys;

    public SessionMembership Membership { get; }

    public SessionAudience Audience => _admissions.Audience;

    public AdmissionDesk Admissions => _admissions.Desk;

    public MemberDrops Drops => _admissions.Drops;

    public IReadOnlyList<PendingAdmission> JustLapsed => _admissions.JustLapsed;

    public void StartHosting() => _hosting.Start();

    public void StopHosting(DateTimeOffset endedAt)
    {
        _roster.PublishClosing(SessionClosing.DecidedByHost(endedAt));
        _hosting.Stop();
    }

    public void RequestJoin(SessionCode code) => RequestJoin(code, DisplayName.None);

    public void RequestJoin(SessionCode code, DisplayName name) => RequestJoin(code, name, null);

    public void RequestJoin(SessionCode code, DisplayName name, Guid? claimedParticipantId)
    {
        _stream.Clear();
        Membership.Undelivered = 0;
        _joiner.Request(code, name, claimedParticipantId);
    }

    public void ReceiveJoinRequest(PendingAdmission request) => _admissions.Receive(request);

    public PendingAdmission? ReceiveJoinRequest(
        PeerCode peerCode,
        byte[] joinerPublicKey,
        DateTimeOffset now,
        RelinkClaim relink = default,
        DisplayName displayName = default) =>
        _admissions.Receive(peerCode, joinerPublicKey, now, relink, displayName);

    public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player, bool asClaimed = false)
    {
        var peer = _admissions.Admit(peerCode, role, asClaimed);

        _roster.Publish();
        return peer;
    }

    public void Deny(PeerCode peerCode) => _admissions.Deny(peerCode);

    public bool CanAdmitAsClaimed(PendingAdmission request) => _admissions.CanAdmitAsClaimed(request);

    public void SynchroniseTransport() => _parts.SynchroniseTransport();

    public void Tick(TimeSpan sinceLastTick, DateTimeOffset now)
    {
        _interruption.ApplyReportedFailure();
        Membership.SessionKey = _parts.Inbox.Drain(
            Join,
            Membership.Keys,
            Host,
            new InboundWiring(_admissions, _resources, _resolveRelink, _roster, Reclaimed, ReclaimRefused, HostWentAway, HostCameBack)
                .For(now, Membership.SessionKey, content => HeardFromTheHost(content)),
            _log)
            ?? Membership.SessionKey;
        RetryAStrandedResume(sinceLastTick);
        _handshake.SendWhatIsDue();
        Membership.FlushWaiting();
        if (Join.Phase is JoinPhase.Failed or JoinPhase.Idle)
        {
            Membership.AbandonWaiting();
        }

        _admissions.ExpireLapsed(now);
        Membership.ExpireIfTheSessionHasClosed(now);

        if (_interruption.Tick(sinceLastTick))
        {
            if (InAHostedSession)
            {
                StopHosting(now);
            }
            else if (Join.Phase == JoinPhase.Admitted)
            {
                Join.Fail(SessionFailure.HostGone);
                SynchroniseTransport();
            }

            return;
        }

        if (_interruption.Reconnecting && !_link.IsReadyToSend)
        {
            if (_reconnect.Due(sinceLastTick))
            {
                SynchroniseTransport();
            }
        }
        else
        {
            _reconnect.Reset();
        }

        if (_timeouts.Advance(sinceLastTick, Host, Join, _handshake.RegistrationWasSent))
        {
            SynchroniseTransport();
        }
    }

    private void RetryAStrandedResume(TimeSpan sinceLastTick)
    {
        if (Join.Phase != JoinPhase.Admitted
            || !Join.Resuming
            || !_interruption.Grace.IsRunning
            || !_link.IsReadyToSend)
        {
            _sinceResumeSent = TimeSpan.Zero;
            return;
        }

        _sinceResumeSent += sinceLastTick;
        if (_sinceResumeSent >= ResumeRetryInterval)
        {
            _sinceResumeSent = TimeSpan.Zero;
            _handshake.ResendResume();
        }
    }

    private void Reclaimed()
    {
        _interruption.HostReconnected();
        _admissions.ForgetPending();
        _roster.Publish();
    }

    private void ReclaimRefused()
    {
        _hosting.Stop();
        _parts.Host.Fail(SessionFailure.ConnectionLost);
    }

    private bool InAJoinedSessionAsPlayer => !InAHostedSession && Join.Phase == JoinPhase.Admitted;

    private void HostWentAway()
    {
        if (InAJoinedSessionAsPlayer)
        {
            _interruption.Grace.HostLost();
        }
    }

    private void HostCameBack()
    {
        if (!InAJoinedSessionAsPlayer)
        {
            return;
        }

        _interruption.Grace.HostReturned();
        _handshake.ResendResume();
    }

    public GraceWindow Grace => _interruption.Grace;

    public string? ReconnectingLine =>
        ReconnectNotice.For(InAHostedSession, _interruption.Grace, _interruption.Seat);

    public bool InAJoinedSession => _interruption.InAJoinedSession;

    public bool InAHostedSession => _parts.Liveness.InAHostedSession;

    public void Fail(SessionFailure failure) => _interruption.Fail(failure);

    public void HostReconnected() => _interruption.HostReconnected();

    public void HostReconnectedWithNewCode() => _interruption.HostReconnectedWithNewCode();

    public void Detach() => _link.Detach();

    private void HeardFromTheHost(SessionContent content)
    {
        _received.Replace(content.Roster);
        _stream.Add(content.Entries);
        Membership.HeardFromTheHost(content.ClosingAtUtcTicks);
        if (!InAHostedSession)
        {
            _interruption.Grace.HostReturned();
        }
    }

}
