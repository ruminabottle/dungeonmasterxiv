using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Drives the session layer: hosting, joining, admission and the relay connection that serves them.
/// Dalamud-free, so the behaviour R-1.1 and R-1.3 specify is testable without a game or a socket.
/// </summary>
public sealed class SessionCoordinator
{
    private readonly RelayLink _link;

    /// <param name="transport">The socket adapter.</param>
    /// <param name="relayAddress">
    /// Reads the configured relay at the moment of connecting rather than at construction, so
    /// changing it in settings takes effect on the next session without a reload (R-1.8).
    /// </param>
    public SessionCoordinator(ISessionTransport transport, Func<string> relayAddress)
    {
        _link = new RelayLink(transport, relayAddress, _inbox.Receive);
        _announcer = new AdmissionAnnouncer(transport);
    }

    private readonly AdmissionAnnouncer _announcer;
    private readonly AdmissionInbox _inbox = new();
    private TimeSpan _timeInPhase;
    private HostingPhase _tickedHostPhase = HostingPhase.NotHosting;
    private JoinPhase _tickedJoinPhase = JoinPhase.Idle;
    private string? _requestedCode;

    /// <summary>The DM's hosting lifecycle.</summary>
    public HostSession Host { get; } = new();

    /// <summary>This client's attempt to join someone else's session.</summary>
    public JoinAttempt Join { get; } = new();

    /// <summary>Who may receive session state. See <see cref="SessionAudience"/> for the D-13 levels.</summary>
    public SessionAudience Audience { get; } = new();

    /// <summary>The requests waiting on the DM (R-1.3).</summary>
    public AdmissionDesk Admissions { get; } = new();

    /// <summary>
    /// This host's ephemeral key pair for the running session, or null when not hosting.
    /// </summary>
    /// <remarks>
    /// Created when hosting starts and disposed when it ends, so it never outlives the session —
    /// D-8 forbids an identifier that links a player across two session codes, and a key pair that
    /// survived would be one.
    /// </remarks>
    public SessionKeyExchange? HostKeys { get; private set; }

    /// <summary>This client's key pair when joining somebody else's session, or null.</summary>
    public SessionKeyExchange? JoinerKeys { get; private set; }

    /// <summary>
    /// The key this client derived on being admitted, or null. Present only once the host's key has
    /// arrived — which is why the acceptance has to carry it.
    /// </summary>
    public byte[]? SessionKey { get; private set; }

    /// <summary>How long this client holds a session after losing the host (R-1.4).</summary>
    public GraceWindow Grace { get; } = new();

    /// <summary>
    /// Participants whose request lapsed on the most recent tick, so the caller can tell them it
    /// lapsed rather than leaving them waiting — and, per R-1.3c, never tell them they were denied.
    /// </summary>
    public IReadOnlyList<PendingAdmission> JustLapsed { get; private set; } = Array.Empty<PendingAdmission>();

    /// <summary>Starts hosting under a freshly generated code (R-1.1, R-1.2a).</summary>
    public void StartHosting()
    {
        HostKeys?.Dispose();
        HostKeys = new SessionKeyExchange();
        Host.Start(SessionCodeGenerator.Next());
        _requestedCode = null;
        SynchroniseTransport();
    }

    /// <summary>
    /// Ends the session. R-1.1 makes this the same path as closing or unloading the plugin, so the
    /// connection cannot outlive the session by taking a different exit.
    /// </summary>
    public void StopHosting()
    {
        Host.Stop();
        HostKeys?.Dispose();
        HostKeys = null;
        Audience.Clear();
        Admissions.Clear();
        _inbox.Clear();
        Grace.Reset();
        JustLapsed = Array.Empty<PendingAdmission>();
        _requestedCode = null;
        SynchroniseTransport();
    }

    /// <summary>Requests to join <paramref name="code"/>. A human action (R-1.3).</summary>
    public void RequestJoin(SessionCode code)
    {
        JoinerKeys?.Dispose();
        JoinerKeys = new SessionKeyExchange();
        SessionKey = null;
        Join.Request(code);
        SynchroniseTransport();
    }

    /// <summary>Records that a participant is asking to be let in.</summary>
    public void ReceiveJoinRequest(PendingAdmission request) => Admissions.Receive(request);

    /// <summary>
    /// Builds and records a request from what arrived on the wire.
    /// </summary>
    /// <remarks>
    /// The fingerprint is computed here rather than passed in, so no caller can hand the prompt a
    /// string that does not correspond to the keys actually exchanged — a fingerprint that does not
    /// match the keys is worse than none, because the DM compares it and concludes it is safe.
    /// The deadline is decided here too: R-1.3c puts that decision on the DM's client (D-3), once.
    /// </remarks>
    public PendingAdmission? ReceiveJoinRequest(
        string peerCode,
        byte[] joinerPublicKey,
        DateTimeOffset now,
        RelinkClaim relink = default)
    {
        if (HostKeys is null)
        {
            return null;
        }

        var deadline = AdmissionDeadline.DecidedByHost(now);
        var request = new PendingAdmission(
            peerCode,
            KeyFingerprint.Of(joinerPublicKey, HostKeys.PublicKey),
            deadline,
            relink,
            joinerPublicKey);

        Admissions.Receive(request);

        // The host's key goes back NOW, not on acceptance (R-1.3a-i, A-1.3f-1). Sending it here is
        // the entire fix: the joiner needs it while the DM is still deciding, because a fingerprint
        // that arrives with the answer cannot inform the answer. The same key travels again in
        // Admit's acceptance envelope, which is where it used to travel for the first time.
        if (Host.Code is { } hostedCode)
        {
            _announcer.Pending(hostedCode, joinerPublicKey, HostKeys.PublicKey, deadline);
        }

        return request;
    }

    /// <summary>
    /// Admits the pending participant. Only after this does anything become addressable to them —
    /// see <see cref="SessionAudience"/>, which is where D-13's None level is enforced.
    /// </summary>
    /// <param name="peerCode">The requester's session-scoped code.</param>
    /// <param name="role">What they may do (E-11). Admission itself stays DM-only.</param>
    /// <remarks>
    /// Whether the DM compared the fingerprint is taken from the request rather than passed in, so
    /// an admission cannot be recorded as verified unless the DM actually said so (R-1.3a).
    /// </remarks>
    public AdmittedPeer Admit(string peerCode, SessionRole role = SessionRole.Player)
    {
        var request = Admissions.Decide(peerCode);
        var peer = Audience.Admit(peerCode, role, request?.Verification ?? AdmissionVerification.NotCompared);

        if (Host.Code is { } code && HostKeys is not null && request?.JoinerPublicKey is { } joinerKey)
        {
            _announcer.Accepted(code, joinerKey, HostKeys.PublicKey);
        }

        return peer;
    }

    /// <summary>
    /// Declines the pending participant. Nothing was ever addressable to them, so there is nothing
    /// to withdraw — which is the point of admitting rather than filtering (R-1.3, D-13).
    /// </summary>
    public void Deny(string peerCode)
    {
        var request = Admissions.Decide(peerCode);
        Audience.Remove(peerCode);

        if (Host.Code is { } code && request?.JoinerPublicKey is { } joinerKey)
        {
            _announcer.Denied(code, joinerKey);
        }
    }

    /// <summary>
    /// The relay answered again after a drop and confirmed we still hold our code.
    /// </summary>
    public void HostReconnected()
    {
        Grace.HostReturned();
    }

    /// <summary>
    /// The relay answered again after a drop but refused the code — somebody claimed it while we
    /// were gone.
    /// </summary>
    /// <remarks>
    /// This is the gap R-1.4 opens and the relay cannot close: it frees a code the moment a host
    /// disconnects, while the grace window keeps the session alive for two minutes. R-1.2a's
    /// regenerate-and-retry then hands us a different code, and without this the DM would carry on
    /// hosting under it while every player still holds the old one — nothing erroring, nothing
    /// looking wrong, and the session simply unjoinable.
    /// </remarks>
    public void HostReconnectedWithNewCode()
    {
        Grace.HostReturned();
        Host.CodeSuperseded(SessionCodeGenerator.Next());
    }

    /// <summary>Reports a transport failure against whichever side of the session is active.</summary>
    public void Fail(SessionFailure failure)
    {
        // R-1.4: losing the host is not the end of the session, it is the start of a grace window.
        // Clients hold their last state and show plainly that it is no longer live; only expiry
        // ends things. Treating a dropped connection as an immediate end is the "instant kick" the
        // product decision rules out.
        if (failure == SessionFailure.ConnectionLost && Host.Phase == HostingPhase.Hosting)
        {
            Grace.HostLost();
            return;
        }

        if (Host.Phase is HostingPhase.Registering or HostingPhase.Hosting)
        {
            Host.Fail(failure);
        }

        if (Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted)
        {
            Join.Fail(failure);
        }

        SynchroniseTransport();
    }

    /// <summary>
    /// Brings the socket into line with whether a session needs one.
    /// </summary>
    /// <remarks>
    /// R-1.1's invariant lives here and in <see cref="HostSession.RequiresRelayConnection"/> and
    /// nowhere else, so there is one answer to "should we be connected" rather than a rule each
    /// call site is trusted to remember.
    /// </remarks>
    public void SynchroniseTransport()
    {
        // The link reports rather than applies, so the mutual recursion between this and Fail still
        // terminates the way it always has: Fail leaves nothing wanting a connection, so the next
        // call through here disconnects and returns None.
        var failure = _link.Synchronise(Host.RequiresRelayConnection || JoinNeedsConnection());

        if (failure != SessionFailure.None)
        {
            Fail(failure);
        }
    }

    /// <summary>
    /// Advances anything that depends on the passage of time. Called once per frame from the
    /// plugin, which is the only place that knows what a frame is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the half that makes A-1.5b real rather than merely correct. The state machines have
    /// always known how to time out; without something calling them every frame,
    /// <see cref="HostingPhase.Registering"/> and <see cref="JoinPhase.Contacting"/> are terminal in
    /// the running product and the user watches the open-ended spinner R-1.8 forbids.
    /// </para>
    /// <para>
    /// Core stays clock-free: the caller supplies the delta, so nothing here reads a clock and every
    /// timeout remains drivable from a test with an explicit <see cref="TimeSpan"/>.
    /// </para>
    /// </remarks>
    /// <param name="sinceLastTick">Elapsed time since the previous call.</param>
    /// <param name="now">
    /// The current instant, for deadlines that were decided elsewhere. Passed in rather than read,
    /// so a lapse is drivable from a test without waiting fifteen minutes — and so this type never
    /// starts a clock of its own, which is what R-1.3c forbids.
    /// </param>
    public void Tick(TimeSpan sinceLastTick, DateTimeOffset now)
    {
        ApplyReportedFailure();
        SessionKey = _inbox.Drain(Join, JoinerKeys, Host) ?? SessionKey;
        RegisterWithRelayWhenReady();
        JustLapsed = Admissions.ExpireLapsed(now);
        AnnounceLapsed();

        if (Grace.Tick(sinceLastTick))
        {
            StopHosting();
            return;
        }


        if (Host.Phase != _tickedHostPhase || Join.Phase != _tickedJoinPhase)
        {
            _tickedHostPhase = Host.Phase;
            _tickedJoinPhase = Join.Phase;
            _timeInPhase = TimeSpan.Zero;
            return;
        }

        _timeInPhase += sinceLastTick;

        var expired = Host.ExpireIfRegistrationTimedOut(_timeInPhase);
        expired |= Join.ExpireIfContactTimedOut(_timeInPhase);

        if (expired)
        {
            SynchroniseTransport();
        }
    }

    /// <summary>
    /// Claims the session's code with the relay, once the socket can actually carry the request
    /// (R-1.2a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the step BUG-36 was missing entirely.</b> <c>WireEnvelope.ForCodeRequest</c> had no
    /// production call site at all: the host connected, sent nothing, and sat in
    /// <see cref="HostingPhase.Registering"/> until it timed out and told the DM the relay was
    /// unreachable — while the relay held the connection open waiting for the client to speak first.
    /// </para>
    /// <para>
    /// <b>On readiness, not on connection, and the difference is the whole reason this is here
    /// rather than in <see cref="SynchroniseTransport"/>.</b>
    /// <see cref="ISessionTransport.Send"/> discards a frame that arrives before the socket opens,
    /// and <see cref="ISessionTransport.IsConnected"/> is already true while a connect is in flight.
    /// Sending on the return from <c>Connect</c> would therefore have produced the same silence
    /// through a different door — and left a fix that looked right in review and failed in the
    /// product.
    /// </para>
    /// <para>
    /// Guarded by <b>which code was requested</b> rather than by a "have we sent one" flag. R-1.2a
    /// answers a refusal by regenerating and asking again, so the interesting question is whether
    /// the code currently held has been claimed — a boolean would be true after the refused attempt
    /// and the replacement code would never be requested.
    /// </para>
    /// </remarks>
    private void RegisterWithRelayWhenReady()
    {
        if (Host.Phase != HostingPhase.Registering
            || Host.Code is not { } code
            || string.Equals(_requestedCode, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _requestedCode = code.Value;
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForCodeRequest(code)));
    }

    /// <summary>Unsubscribes from the transport. Wired into the plugin's teardown.</summary>
    public void Detach() => _link.Detach();

    private void ApplyReportedFailure()
    {
        if (_link.TryTakeReportedFailure(out var failure))
        {
            Fail(failure);
        }
    }

    private void AnnounceLapsed()
    {
        if (Host.Code is { } code)
        {
            _announcer.Lapsed(code, JustLapsed);
        }
    }

    private bool JoinNeedsConnection() =>
        Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;
}
