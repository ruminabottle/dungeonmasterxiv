using System;
using System.Security.Cryptography;
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
    /// <param name="newKeys">
    /// How a session key pair is made. Injected so a failure to make one can be driven from a test
    /// (BUG-61): on the machine that reported it, this throws, and there was no seam between that
    /// throw and the frame loop.
    /// </param>
    /// <param name="window">
    /// How long a session survives an interruption, from settings (A-1.23, A-1.27). Required, so no
    /// caller can silently fall back to the literal — see <c>SessionInterruption</c>'s remark.
    /// </param>
    public SessionCoordinator(
        ISessionTransport transport,
        Func<string> relayAddress,
        TimeSpan window,
        Func<SessionKeyExchange>? newKeys = null)
    {
        _newKeys = newKeys ?? (static () => new SessionKeyExchange());
        _link = new RelayLink(transport, relayAddress, _inbox.Receive);
        _admissions = new AdmissionControl(
            new AdmissionAnnouncer(transport),
            () => Host.Code,
            () => HostKeys);
        // Null-conditional because _joiner is built two lines below this one: the closure is not
        // INVOKED until after construction, but the compiler cannot know that, and "no joiner keys
        // yet" is the honest answer for the window in which it could be. Suppressing with ! would
        // have asserted something this constructor does not yet guarantee.
        _handshake = new OutboundHandshake(_link, Host, Join, () => _joiner?.Keys);
        _roster = new RosterBroadcast(_link, Audience, () => HostKeys, () => Host.Code);
        _interruption = new SessionInterruption(_link, Host, Join, SynchroniseTransport, window);
        _joiner = new JoinRequester(_handshake, _interruption, Join, _newKeys, SynchroniseTransport);
    }

    private readonly Func<SessionKeyExchange> _newKeys;
    private readonly AdmissionControl _admissions;
    private readonly AdmissionInbox _inbox = new();
    private readonly OutboundHandshake _handshake;
    private readonly RosterBroadcast _roster;
    private readonly SessionInterruption _interruption;
    private readonly JoinRequester _joiner;
    private IReadOnlyList<RosterEntry> _receivedRoster = [];
    private TimeSpan _timeInPhase;
    private HostingPhase _tickedHostPhase = HostingPhase.NotHosting;
    private JoinPhase _tickedJoinPhase = JoinPhase.Idle;

    /// <summary>
    /// Who this client believes is in the session (R-1.3f).
    /// </summary>
    /// <remarks>
    /// <b>On the HOST this stays empty, and that is not an oversight.</b> The host authors the
    /// roster from <see cref="Audience"/> and never receives one — D-3 makes it the author, so a
    /// host reading its own broadcast back would be believing a copy of what it already knows. This
    /// is what a PLAYER was told, which is the only place the distinction matters.
    /// <para>
    /// <b>Replaced, never merged.</b> A participant who left is gone because the next roster does
    /// not list them, rather than lingering until a removal message that may never arrive.
    /// </para>
    /// </remarks>
    public IReadOnlyList<RosterEntry> Roster => _receivedRoster;

    /// <summary>The DM's hosting lifecycle.</summary>
    public HostSession Host { get; } = new();

    /// <summary>This client's attempt to join someone else's session.</summary>
    public JoinAttempt Join { get; } = new();



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
    /// <remarks>Owned by <see cref="JoinRequester"/>, which is the only thing that creates it.</remarks>
    public SessionKeyExchange? JoinerKeys => _joiner.Keys;

    /// <summary>
    /// The key this client derived on being admitted, or null. Present only once the host's key has
    /// arrived — which is why the acceptance has to carry it.
    /// </summary>
    public byte[]? SessionKey => _joiner.SessionKey;

    /// <summary>Who may receive session state. See <see cref="SessionAudience"/> for the D-13 levels.</summary>
    public SessionAudience Audience => _admissions.Audience;

    /// <summary>The requests waiting on the DM (R-1.3).</summary>
    public AdmissionDesk Admissions => _admissions.Desk;

    /// <summary>
    /// Participants whose request lapsed on the most recent tick, so the caller can tell them it
    /// lapsed rather than leaving them waiting — and, per R-1.3c, never tell them they were denied.
    /// </summary>
    public IReadOnlyList<PendingAdmission> JustLapsed => _admissions.JustLapsed;



    /// <summary>Starts hosting under a freshly generated code (R-1.1, R-1.2a).</summary>
    public void StartHosting()
    {
        HostKeys?.Dispose();
        HostKeys = null;

        // BUG-61. This throws on at least one real machine, and it used to unwind out of the button
        // handler and out of Draw -- so the user got an exception every frame rather than an answer
        // once. Caught HERE rather than at the button, because both of the product's two entry
        // points construct a key pair and a guard at one of them leaves the other open.
        if (!SessionKeyPair.TryMake(_newKeys, out var hostKeys))
        {
            Host.Fail(SessionFailure.SessionKeysUnavailable);
            return;
        }

        HostKeys = hostKeys;
        Host.Start(SessionCodeGenerator.Next());
        _handshake.ForgetHostRegistration();
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
        _admissions.Clear();
        _inbox.Clear();
        Grace.Reset();
        _handshake.ForgetHostRegistration();
        SynchroniseTransport();
    }

    /// <summary>Requests to join <paramref name="code"/>. A human action (R-1.3).</summary>
    public void RequestJoin(SessionCode code) => RequestJoin(code, DisplayName.None);

    /// <summary>
    /// Requests to join <paramref name="code"/>, naming ourselves (R-1.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name is taken here rather than read from anywhere, because Core cannot see the game: the
    /// character name is a Dalamud read and lives behind a plugin-side seam, the same shape as
    /// <c>ICampaignStoreLog</c>. Core stays testable and the name arrives as a value.
    /// </para>
    /// <para>
    /// <b>Held for the send, not for the session.</b> It travels on the JoinRequest and nothing
    /// else consults it, so a joiner never accumulates an identity here — D-8 keeps names
    /// campaign-scoped and out of exports.
    /// </para>
    /// </remarks>
    /// <param name="code">The session to ask to join.</param>
    /// <param name="name">What to call ourselves in the DM's prompt. Never authenticates.</param>
    public void RequestJoin(SessionCode code, DisplayName name) => RequestJoin(code, name, null);

    /// <summary>
    /// Requests to join <paramref name="code"/>, claiming a participant we believe is ours (R-1.5).
    /// </summary>
    /// <remarks>
    /// <b>A forwarder since DMXENG-31, and deliberately still HERE.</b> The sequence lives on
    /// <see cref="JoinRequester"/>; this signature stays because PR #75's A-1.12a table drives
    /// production through it and carries an approve-blocking gate. A split is not a licence to move
    /// somebody else's entry point.
    /// </remarks>
    /// <param name="code">The session to ask to join.</param>
    /// <param name="name">What to call ourselves. Never authenticates.</param>
    /// <param name="claimedParticipantId">The participant we claim, or null for an ordinary join.</param>
    public void RequestJoin(SessionCode code, DisplayName name, Guid? claimedParticipantId) =>
        _joiner.Request(code, name, claimedParticipantId);

    /// <summary>Records that a participant is asking to be let in.</summary>
    public void ReceiveJoinRequest(PendingAdmission request) => _admissions.Receive(request);

    /// <summary>Builds and records a request from what arrived on the wire.</summary>
    /// <param name="peerCode">The requester's session-scoped code.</param>
    /// <param name="joinerPublicKey">The key they presented (D-11).</param>
    /// <param name="now">The current instant, for the admission deadline.</param>
    /// <param name="relink">What this client resolved about a claimed participant, if anything.</param>
    /// <param name="displayName">What they call themselves (R-1.3e). Shown, never acted on.</param>
    public PendingAdmission? ReceiveJoinRequest(
        PeerCode peerCode,
        byte[] joinerPublicKey,
        DateTimeOffset now,
        RelinkClaim relink = default,
        DisplayName displayName = default) =>
        _admissions.Receive(peerCode, joinerPublicKey, now, relink, displayName);

    /// <summary>Admits the pending participant (R-1.3, D-13).</summary>
    public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player)
    {
        var peer = _admissions.Admit(peerCode, role);

        // Published on the ADMISSION rather than on the roster changing, which is what A-1.13a
        // needs: a client reconnecting mid-session is re-admitted and must receive the CURRENT
        // roster even though nothing about the membership is new to the host.
        _roster.Publish();
        return peer;
    }

    /// <summary>Declines the pending participant (R-1.3, D-13).</summary>
    public void Deny(PeerCode peerCode) => _admissions.Deny(peerCode);


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
        _interruption.ApplyReportedFailure();
        _joiner.SessionKey = _inbox.Drain(
            Join,
            _joiner.Keys,
            Host,
            new InboundHandlers(
                OnJoinRequest: (key, name) => _admissions.AdmitToTheQueue(key, now, name),
                OpenWith: SessionKey,
                OnContent: content => _receivedRoster = content.Roster ?? _receivedRoster))
            ?? SessionKey;
        _handshake.SendWhatIsDue();
        _admissions.ExpireLapsed(now);

        if (_interruption.Tick(sinceLastTick))
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

        // Whether we ever got to speak, which is the difference between "the relay heard us and
        // said nothing" and "we never reached the relay" (BUG-38). It lives on the handshake now,
        // because the handshake is what knows whether the request left.
        var expired = Host.ExpireIfRegistrationTimedOut(_timeInPhase, _handshake.RegistrationWasSent);
        expired |= Join.ExpireIfContactTimedOut(_timeInPhase);

        if (expired)
        {
            SynchroniseTransport();
        }
    }


    /// <summary>How long this client holds a session after losing the host (R-1.4).</summary>
    /// <remarks>
    /// Owned by <see cref="SessionInterruption"/>, which is where a dropped link is turned into a
    /// window rather than an ending. Exposed here because the window this client draws reads it.
    /// </remarks>
    public GraceWindow Grace => _interruption.Grace;

    /// <summary>
    /// Whether this client is in a joined session, including one whose link dropped but whose seat
    /// is still resumable (R-1.3h, BUG-53). The window asks this rather than reading a phase.
    /// </summary>
    public bool InAJoinedSession => _interruption.InAJoinedSession;

    /// <summary>Reports a transport failure against whichever side of the session is active.</summary>
    /// <param name="failure">What the transport reported.</param>
    public void Fail(SessionFailure failure) => _interruption.Fail(failure);

    /// <summary>The relay answered again after a drop and confirmed we still hold our code.</summary>
    public void HostReconnected() => _interruption.HostReconnected();

    /// <summary>
    /// The relay answered again after a drop but refused the code — somebody claimed it while we
    /// were gone.
    /// </summary>
    public void HostReconnectedWithNewCode() => _interruption.HostReconnectedWithNewCode();

    /// <summary>Unsubscribes from the transport. Wired into the plugin's teardown.</summary>
    public void Detach() => _link.Detach();



    private bool JoinNeedsConnection() =>
        Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;
}
