using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Runs the host's side of admission: queues join requests, admits or denies them, and tracks drops.</summary>
public sealed class AdmissionControl
{
    private readonly AdmissionAnnouncer _announcer;
    private readonly Func<DisplayName, Guid?> _mintParticipant;
    private readonly Func<bool> _letReturningPlayersIn;
    private readonly ISessionTransportLog _log;
    private readonly Func<SessionCode?> _hostCode;
    private readonly Func<SessionKeyExchange?> _hostKeys;

    public AdmissionControl(
        AdmissionAnnouncer announcer,
        Func<SessionCode?> hostCode,
        Func<SessionKeyExchange?> hostKeys,
        Func<DisplayName, Guid?> mintParticipant,
        Func<bool> letReturningPlayersIn,
        ISessionTransportLog log)
    {
        ArgumentNullException.ThrowIfNull(mintParticipant);
        ArgumentNullException.ThrowIfNull(letReturningPlayersIn);
        ArgumentNullException.ThrowIfNull(log);

        _announcer = announcer;
        _hostCode = hostCode;
        _hostKeys = hostKeys;
        _mintParticipant = mintParticipant;
        _letReturningPlayersIn = letReturningPlayersIn;
        _log = log;
        DropLines = new DropAnnouncements(Drops);
    }

    public SessionAudience Audience { get; } = new();

    public AdmissionDesk Desk { get; } = new();

    public IReadOnlyList<PendingAdmission> JustLapsed { get; private set; } = Array.Empty<PendingAdmission>();

    public void Receive(PendingAdmission request) => Desk.Receive(request);

    public PendingAdmission? AdmitToTheQueue(
        byte[] joinerPublicKey,
        DateTimeOffset now,
        DisplayName displayName = default,
        RelinkClaim relink = default,
        bool supportsAudiences = false) =>
        Receive(PeerCodeFor(joinerPublicKey), joinerPublicKey, now, relink, displayName, supportsAudiences);

    public PeerCode PeerCodeFor(byte[] joinerPublicKey)
    {
        var scope = System.Text.Encoding.UTF8.GetBytes(_hostCode()?.Value ?? string.Empty);
        var digest = System.Security.Cryptography.SHA256.HashData([.. scope, .. joinerPublicKey]);
        var value = new System.Numerics.BigInteger(digest, isUnsigned: true, isBigEndian: true);

        var rendered = new char[SessionCode.Length];
        for (var i = rendered.Length - 1; i >= 0; i--)
        {
            value = System.Numerics.BigInteger.DivRem(value, SpeakableAlphabet.Length, out var symbol);
            rendered[i] = SpeakableAlphabet.Characters[(int)symbol];
        }

        return PeerCode.FromGenerated(new string(rendered));
    }

    public PendingAdmission? Receive(
        PeerCode peerCode,
        byte[] joinerPublicKey,
        DateTimeOffset now,
        RelinkClaim relink = default,
        DisplayName displayName = default,
        bool supportsAudiences = false)
    {
        if (_hostKeys() is not { } hostKeys)
        {
            return null;
        }

        if (Desk.Find(peerCode) is { } existing)
        {
            return existing;
        }

        var deadline = AdmissionDeadline.DecidedByHost(now);
        var request = new PendingAdmission(peerCode, deadline, relink, joinerPublicKey, displayName, supportsAudiences);

        Desk.Receive(request);

        if (_hostCode() is { } hostedCode)
        {
            _announcer.Pending(hostedCode, joinerPublicKey, hostKeys.PublicKey, deadline);
        }

        return request;
    }

    public MemberDrops Drops { get; } = new();

    public DropAnnouncements DropLines { get; }

    /// <summary>Records an admitted member's drop; a drop already recorded keeps its start time.</summary>
    public bool RecordDrop(byte[] memberPublicKey, DateTimeOffset when)
    {
        ArgumentNullException.ThrowIfNull(memberPublicKey);

        var peerCode = PeerCodeFor(memberPublicKey);
        if (!Audience.IsAdmitted(peerCode) || Drops.WhenDropped(peerCode) is not null)
        {
            return false;
        }

        Drops.Record(peerCode, when);
        return true;
    }

    public void OfferHostKey(byte[] joinerPublicKey)
    {
        if (_hostCode() is { } code && _hostKeys() is { } hostKeys)
        {
            _announcer.HostKey(code, joinerPublicKey, hostKeys.PublicKey);
        }
    }

    public (PeerCode Peer, long LastSequence)? Resume(byte[] memberPublicKey, WireEnvelope envelope)
    {
        if (_hostCode() is not { } code || _hostKeys() is not { } hostKeys)
        {
            return null;
        }

        var peer = PeerCodeFor(memberPublicKey);
        if (!Audience.IsAdmitted(peer))
        {
            _announcer.Denied(code, memberPublicKey);
            return null;
        }

        byte[] key;
        try
        {
            key = hostKeys.DeriveSharedKey(memberPublicKey, code);
        }
        catch (CryptographicException)
        {
            return null;
        }

        var details = JoinDetailsCodec.TryOpen(key, envelope);
        CryptographicOperations.ZeroMemory(key);

        if (details is null)
        {
            return null;
        }

        Audience.NoteAudiences(peer, details.Audiences ?? false);
        Drops.Forget(peer);
        _announcer.Accepted(code, memberPublicKey, hostKeys.PublicKey);
        return (peer, details.LastSequence ?? 0);
    }

    public JoinDetails? OpenJoinRequest(byte[] joinerPublicKey, WireEnvelope envelope)
    {
        if (_hostCode() is not { } code || _hostKeys() is not { } hostKeys)
        {
            return null;
        }

        byte[] key;
        try
        {
            key = hostKeys.DeriveSharedKey(joinerPublicKey, code);
        }
        catch (CryptographicException)
        {
            return null;
        }

        var details = JoinDetailsCodec.TryOpen(key, envelope);
        CryptographicOperations.ZeroMemory(key);

        if (details is null)
        {
            _log.Warning(
                "A join request arrived that could not be opened with the key agreed for it, so it was "
                + "discarded. A joiner on a different build and something altering the request on the "
                + "way look the same from here.");
        }

        return details;
    }

    public bool LetsInAutomatically(PendingAdmission request) =>
        _letReturningPlayersIn() && ClaimedAndFree(request) is not null;

    public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player, bool asClaimed = false)
    {
        var request = Desk.Decide(peerCode);
        var displayName = request?.DisplayName ?? DisplayName.None;

        Guid? participantId;
        if (asClaimed && ClaimedAndFree(request) is { } claimed)
        {
            if (Audience.HolderOf(claimed) is { } holder)
            {
                Audience.Remove(holder.PeerCode);
                ForgetDrop(holder.PeerCode);
            }

            participantId = claimed;
        }
        else
        {
            participantId = _mintParticipant(displayName);
        }

        var peer = Audience.Admit(peerCode, role, request?.JoinerPublicKey, displayName, participantId);
        Audience.NoteAudiences(peerCode, request?.SupportsAudiences ?? false);

        ForgetDrop(peerCode);
        AnnounceAccepted(request?.JoinerPublicKey, participantId);

        if (participantId is null)
        {
            _log.Warning(
                $"Admitted {peerCode} without creating a participant, so this session has no "
                + "campaign to record them in. They will not be able to relink and will be a new "
                + "request next time.");
        }

        return peer;
    }

    private void ForgetDrop(PeerCode peerCode)
    {
        Drops.Forget(peerCode);
        DropLines.Forget(peerCode);
    }

    public bool CanAdmitAsClaimed(PendingAdmission request) => ClaimedAndFree(request) is not null;

    private Guid? ClaimedAndFree(PendingAdmission? request)
    {
        if (request?.Relink is not { Matched: true, ParticipantId: { } claimed })
        {
            return null;
        }

        var holder = Audience.HolderOf(claimed);
        return holder is null || Drops.WhenDropped(holder.PeerCode) is not null ? claimed : null;
    }

    private void AnnounceAccepted(byte[]? joinerPublicKey, Guid? participantId)
    {
        if (_hostCode() is not { } code || _hostKeys() is not { } hostKeys || joinerPublicKey is null)
        {
            return;
        }

        SealedPayload? welcome = null;
        if (participantId is { } id)
        {
            var key = hostKeys.DeriveSharedKey(joinerPublicKey, code);
            welcome = JoinDetailsCodec.Seal(
                key, new JoinDetails { ParticipantId = id.ToString("D") }, code, WireMessageType.JoinAccepted);
            CryptographicOperations.ZeroMemory(key);
        }

        _announcer.Accepted(code, joinerPublicKey, hostKeys.PublicKey, welcome);
    }

    public bool Departed(PeerCode peerCode)
    {
        ForgetDrop(peerCode);
        return Audience.Remove(peerCode);
    }

    public void Deny(PeerCode peerCode)
    {
        var request = Desk.Decide(peerCode);
        Audience.Remove(peerCode);
        ForgetDrop(peerCode);

        if (_hostCode() is { } code && request?.JoinerPublicKey is { } joinerKey)
        {
            _announcer.Denied(code, joinerKey);
        }
    }

    private void AnnounceLapsed()
    {
        if (_hostCode() is { } code)
        {
            _announcer.Lapsed(code, JustLapsed);
        }
    }
    public void ExpireLapsed(DateTimeOffset now)
    {
        JustLapsed = Desk.ExpireLapsed(now);
        AnnounceLapsed();
    }

    public void ForgetPending() => Desk.Clear();

    public void Clear()
    {
        Audience.Clear();
        Desk.Clear();
        Drops.Clear();
        DropLines.Clear();
        JustLapsed = Array.Empty<PendingAdmission>();
    }
}
