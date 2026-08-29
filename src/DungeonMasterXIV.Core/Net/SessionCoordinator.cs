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
    /// <param name="window">
    /// How long a session survives an interruption, from settings (A-1.23, A-1.27). Required, so no
    /// caller can silently fall back to the literal — see <c>SessionInterruption</c>'s remark.
    /// </param>
    /// <param name="log">
    /// Where content this client accepted but had to strip is reported (BUG-70).
    /// <para>
    /// <b>REQUIRED, and it arrived optional (DMXENG-13).</b> #123 introduced it as
    /// <c>ISessionTransportLog? log = null</c> and argued the default was the silent case. That is
    /// true of what the log DOES and not of who SUPPLIES it: production passes one today because
    /// today's single call site happens to, which is a fact about that call site rather than a
    /// property of this type. <b>An optional parameter production happens to supply is one refactor
    /// away from production not supplying it, and nothing would fail.</b>
    /// </para>
    /// <para>
    /// <b>It used to say it sat here because a required parameter cannot follow an optional one.</b>
    /// That constraint is gone — DMXENG-57 left no optional parameters for it to precede — so the
    /// position is now free and the requiredness is load-bearing on its own. Kept as a correction
    /// rather than deleted, because "required for a C# reason" and "required for DMXENG-13's
    /// reason" look identical in a signature and only one of them survives a reordering.
    /// </para>
    /// </param>
    /// <param name="capabilities">
    /// What Core cannot do for itself — key generation and participant minting. <b>Required, and
    /// a caller wanting the defaults says <see cref="SessionCapabilities.Default"/> out loud</b>
    /// (DMXENG-13). A record rather than parameters so the NEXT capability costs a member here
    /// instead of a seventh argument, which is what stopped two chunks at once (DMXENG-57).
    /// </param>
    public SessionCoordinator(
        ISessionTransport transport,
        Func<string> relayAddress,
        TimeSpan window,
        ISessionTransportLog log,
        SessionCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(capabilities);

        _newKeys = capabilities.KeySource;
        _resolveRelink = capabilities.RelinkSource;
        _log = log;
        _link = new RelayLink(transport, relayAddress, _inbox.Receive);
        _admissions = new AdmissionControl(
            new AdmissionAnnouncer(transport),
            () => Host.Code,
            () => HostKeys,
            capabilities.ParticipantSource,
            log);
        // Null-conditional because _joiner is built FURTHER DOWN this constructor: the closure is
        // not INVOKED until after construction, but the compiler cannot know that. Suppressing with
        // ! would assert something this constructor does not yet guarantee. That reasoning stands.
        //
        // A DIRECTION RATHER THAN A COUNT: this said "two lines below" and #125 made it seven. A
        // distance in prose carries no line number for any grep to find, so it went stale silently.
        //
        // The order itself is now DETECTED (DMXENG-45): JoinRequester guards its collaborators, so
        // building it before these throws rather than passing a null nothing refuses. Measured --
        // with the order swapped and no guard, the suite passed clean.
        _handshake = new OutboundHandshake(_link, Host, Join, () => _joiner?.Keys);
        // The PARAMETER, not the field. Reading _log here would work only because :56 happens
        // to precede this line, and nothing detects a reordering -- which is DMXENG-45's defect
        // exactly. Taking it from the argument removes the ordering dependency instead of
        // relying on it.
        _roster = new RosterBroadcast(_link, Audience, () => HostKeys, () => Host.Code, log);
        // What RosterBroadcast reads to SEAL, read here to OPEN (R-1.3k).
        _resources = new SessionResources(
            _admissions,
            _inbox,
            () => Grace,
            new MemberContentKeys(Audience, () => HostKeys, () => Host.Code, log),
            new MemberContentReceipts());
        _interruption = new SessionInterruption(_link, Host, Join, SynchroniseTransport, window);
        _joiner = new JoinRequester(_handshake, _interruption, Join, _newKeys, SynchroniseTransport);
        // AFTER _interruption, which owns the Grace window this reads. The Func defers that read to
        // use time, so the ordering hazard DMXENG-45 detected does not extend to it -- but HostRunner
        // guards every argument anyway, which is the point of those guards.
        _hosting = new HostRunner(Host, _resources, _handshake, _newKeys, SynchroniseTransport);
    }

    private readonly Func<SessionKeyExchange> _newKeys;
    private readonly Func<string?, RelinkClaim> _resolveRelink;
    private readonly ISessionTransportLog _log;
    private readonly AdmissionControl _admissions;
    private readonly AdmissionInbox _inbox = new();
    private readonly OutboundHandshake _handshake;
    private readonly RosterBroadcast _roster;
    private readonly SessionResources _resources;
    private readonly SessionInterruption _interruption;
    private readonly JoinRequester _joiner;
    private readonly HostRunner _hosting;
    private IReadOnlyList<RosterEntry> _receivedRoster = [];
    private readonly PhaseTimeouts _timeouts = new();

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

    /// <summary>What this host has heard from its members (R-1.3k, A-1.13c).</summary>
    /// <remarks>
    /// The inverse of <see cref="Roster"/>: that is what a host TOLD this client, this is what
    /// members told the HOST. See <see cref="MemberContentReceipts"/> for the rest, including that
    /// nothing shipped sends these yet (DMXENG-11 / A-1.15).
    /// </remarks>
    public MemberContentReceipts MemberContent => _resources.MemberContent;

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
    public SessionKeyExchange? HostKeys => _hosting.Keys;

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
    public void StartHosting() => _hosting.Start();

    /// <summary>
    /// Ends the session. R-1.1 makes this the same path as closing or unloading the plugin, so the
    /// connection cannot outlive the session by taking a different exit.
    /// </summary>
    /// <param name="endedAt">
    /// When the DM ended it — <b>the moment, not a deadline</b>; the window is
    /// <see cref="SessionClosing"/>'s and no caller can choose one. REQUIRED rather than defaulted,
    /// because a default would let a call site end the session sending no notice at all — A-1.16
    /// failing silently with the suite green, where a required parameter fails at compile time.
    /// </param>
    /// <remarks>
    /// <b>The notice goes out BEFORE the delegation, and the order is load-bearing.</b> Teardown
    /// lives inside <see cref="HostRunner.Stop"/> since DMXENG-51 and empties the admissions, so
    /// publishing afterwards seals to nobody and fails silently. Both that and the call's absence
    /// are pinned by <c>EndingASessionAnnouncesItTests</c>, which exists because each mutation left
    /// the whole suite green.
    /// </remarks>
    public void StopHosting(DateTimeOffset endedAt)
    {
        _roster.PublishClosing(SessionClosing.DecidedByHost(endedAt));
        _hosting.Stop();
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
                // T-37: the claim is RESOLVED HERE, at the one place that has both the wire and
                // the campaign. Until now it arrived on the envelope and was dropped -- the joiner
                // sent it, the relay routed it, and every relink branch took the not-a-relink path
                // because Receive was only ever reached with RelinkClaim.None.
                OnJoinRequest: (key, name, claimed) =>
                    _admissions.AdmitToTheQueue(key, now, name, _resolveRelink(claimed)),
                OpenWith: SessionKey,
                OnContent: content => _receivedRoster = content.Roster ?? _receivedRoster,
                OnComparabilityReceipt: _admissions.RecordComparabilityReceipt,
                // R-1.3k. DELIBERATELY NOT THE LAMBDA ABOVE: that is what a JOINER was told, and
                // letting a member reach it would invert D-3 — see InboundHandlers.OnMemberContent.
                OpenMemberContentWith: _resources.MemberKeys.Candidates,
                OnMemberContent: _resources.MemberContent.Record),
            _log)
            ?? SessionKey;
        _handshake.SendWhatIsDue();
        _admissions.ExpireLapsed(now);

        if (_interruption.Tick(sinceLastTick))
        {
            // Lapsed rather than closed by the DM; PublishClosing declines it, see there.
            StopHosting(now);
            return;
        }

        // The phase clock and both expiry checks live in PhaseTimeouts. The return above means
        // "hosting has stopped, abandon the frame"; the one that used to sit below it meant "the
        // phase just changed, nothing can have expired" -- two returns in one method meaning two
        // different things, which is most of why that state is its own type now.
        if (_timeouts.Advance(sinceLastTick, Host, Join, _handshake.RegistrationWasSent))
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
