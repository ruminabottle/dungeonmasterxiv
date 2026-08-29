using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The host's own side: opening and closing a session, and the keys that hosting creates (R-1.1,
/// R-1.2a).
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS THE HALF <see cref="JoinRequester"/> LEFT BEHIND, AND THAT TYPE SAYS SO IN ITS OWN
/// WORDS.</b> DMXENG-31 cut the joining side out of <see cref="SessionCoordinator"/> and recorded
/// why the key-pair helper did not travel with it: <i>"hosting needs it too. It is
/// <see cref="SessionKeyPair"/>, reachable by both and owned by neither."</i> The joining side got a
/// type; the hosting side stayed inline, and has sat in the coordinator by default rather than by
/// design ever since. <b>The asymmetry was the anomaly — this removes it rather than inventing a
/// boundary.</b>
/// </para>
/// <para>
/// The parallel is exact and worth stating so it is not re-derived later:
/// <see cref="JoinAttempt"/> is the joiner's phase machine and <see cref="JoinRequester"/> is what
/// drives it; <see cref="HostSession"/> is the host's phase machine and <b>this</b> is what drives
/// it. Neither driver owns a phase; both own the outbound act and the key pair that act creates.
/// </para>
/// <para>
/// <b>A PURE MOVE. No behaviour changes here, no criterion is claimed, and nothing was fixed on the
/// way past</b> — including the asymmetry named below, which is real and is deliberately left
/// alone. DMXENG-51 exists because <see cref="SessionCoordinator"/> reaches exactly its 400-line
/// block once DMXENG-47 and DMXENG-50 both land, neither of which breaches alone; a refactor that
/// also changed behaviour would make the size question and the behaviour question one review.
/// </para>
/// <para>
/// <b>WHAT THE MOVE MADE VISIBLE, WHICH IS THE REASON IT IS THE RIGHT SEAM RATHER THAN A CHEAP
/// ONE.</b> Putting <see cref="Start"/> and <see cref="Stop"/> beside each other shows that they
/// are <b>not symmetric</b>: <c>Stop</c> tears down seven things and <c>Start</c> resets two.
/// Calling <c>Start</c> without <c>Stop</c> — which nothing forbids — leaves the previous session's
/// admissions, queued frames and grace window in place under a new key pair and a new code. That is
/// reported on DMXENG-51 rather than repaired here, and
/// <c>MemberContentKeys.ForgetIfTheSessionMoved</c> exists because of it. <b>The set of things a
/// hosted session owns had no name anywhere until this type; each method was trusted to remember
/// it.</b>
/// </para>
/// <para>
/// <b><see cref="SessionCoordinator.StartHosting"/> and <see cref="SessionCoordinator.StopHosting"/>
/// remain callable with their current signatures</b>, as thin forwarders — the same fence
/// DMXENG-31 kept, and for a stronger reason here: the plugin's teardown and both session windows
/// call them, and this ticket's boundary does not include the plugin.
/// </para>
/// </remarks>
internal sealed class HostRunner
{
    private readonly HostSession _host;
    private readonly AdmissionControl _admissions;
    private readonly AdmissionInbox _inbox;
    private readonly OutboundHandshake _handshake;
    private readonly Func<GraceWindow> _grace;
    private readonly Func<SessionKeyExchange> _newKeys;
    private readonly Action _synchronise;

    /// <param name="host">The hosting phase machine this drives. Owned by the coordinator.</param>
    /// <param name="admissions">Who is admitted and who is waiting; emptied when the session ends.</param>
    /// <param name="inbox">Queued frames; emptied when the session ends so none crosses into the next.</param>
    /// <param name="handshake">What puts the code request on the wire, and what remembers it was sent.</param>
    /// <param name="grace">
    /// The seat clock, read at use time rather than captured. A <c>Func</c> because it belongs to
    /// <c>SessionInterruption</c> and is reached through the coordinator, so capturing the value
    /// would pin whichever window existed at construction.
    /// </param>
    /// <param name="newKeys">How a key pair is made (BUG-61).</param>
    /// <param name="synchronise">Brings the socket into line once the phase has moved.</param>
    public HostRunner(
        HostSession host,
        AdmissionControl admissions,
        AdmissionInbox inbox,
        OutboundHandshake handshake,
        Func<GraceWindow> grace,
        Func<SessionKeyExchange> newKeys,
        Action synchronise)
    {
        // DMXENG-45's rule, applied to a new constructor rather than rediscovered by it. Several of
        // these arrive from fields assigned earlier in SessionCoordinator's constructor, so building
        // this type too early passes a null that nothing would refuse -- the assignment succeeds and
        // the failure surfaces later, on a hosting path, or never in a test that does not host.
        //
        // ALL SEVEN are guarded rather than only those that are order-sensitive today. Which
        // arguments come from earlier fields is a fact about SessionCoordinator that can change
        // without anyone editing this file.
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(admissions);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(handshake);
        ArgumentNullException.ThrowIfNull(grace);
        ArgumentNullException.ThrowIfNull(newKeys);
        ArgumentNullException.ThrowIfNull(synchronise);

        _host = host;
        _admissions = admissions;
        _inbox = inbox;
        _handshake = handshake;
        _grace = grace;
        _newKeys = newKeys;
        _synchronise = synchronise;
    }

    /// <summary>
    /// This host's ephemeral key pair for the running session, or null when not hosting.
    /// </summary>
    /// <remarks>
    /// <b>The setter came here with the sequence, and that is the seam rather than a widening.</b>
    /// It is written by exactly two places, <see cref="Start"/> and <see cref="Stop"/>, and both are
    /// now here. Leaving it on the coordinator would have meant this type reaching back through a
    /// setter to mutate state it is the only author of — a seam in name and a dependency in fact,
    /// which is the reason <see cref="JoinRequester"/> gives for taking <c>Keys</c> with it.
    /// </remarks>
    public SessionKeyExchange? Keys { get; private set; }

    /// <summary>Starts hosting under a freshly generated code (R-1.1, R-1.2a).</summary>
    public void Start()
    {
        Keys?.Dispose();
        Keys = null;

        // BUG-61. This throws on at least one real machine, and it used to unwind out of the button
        // handler and out of Draw -- so the user got an exception every frame rather than an answer
        // once. Caught HERE rather than at the button, because both of the product's two entry
        // points construct a key pair and a guard at one of them leaves the other open.
        if (!SessionKeyPair.TryMake(_newKeys, out var hostKeys))
        {
            _host.Fail(SessionFailure.SessionKeysUnavailable);
            return;
        }

        Keys = hostKeys;
        _host.Start(SessionCodeGenerator.Next());
        _handshake.ForgetHostRegistration();
        _synchronise();
    }

    /// <summary>
    /// Ends the session. R-1.1 makes this the same path as closing or unloading the plugin, so the
    /// connection cannot outlive the session by taking a different exit.
    /// </summary>
    public void Stop()
    {
        _host.Stop();
        Keys?.Dispose();
        Keys = null;
        _admissions.Clear();
        _inbox.Clear();
        _grace().Reset();
        _handshake.ForgetHostRegistration();
        _synchronise();
    }
}
