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
    private readonly ISessionTransport _transport;
    private readonly Func<string> _relayAddress;

    /// <param name="transport">The socket adapter.</param>
    /// <param name="relayAddress">
    /// Reads the configured relay at the moment of connecting rather than at construction, so
    /// changing it in settings takes effect on the next session without a reload (R-1.8).
    /// </param>
    public SessionCoordinator(ISessionTransport transport, Func<string> relayAddress)
    {
        _transport = transport;
        _relayAddress = relayAddress;
        _transport.Failed += OnTransportFailed;
    }

    private readonly object _reportedFailureLock = new();
    private SessionFailure _reportedFailure = SessionFailure.None;
    private TimeSpan _timeInPhase;
    private HostingPhase _tickedHostPhase = HostingPhase.NotHosting;
    private JoinPhase _tickedJoinPhase = JoinPhase.Idle;

    /// <summary>The DM's hosting lifecycle.</summary>
    public HostSession Host { get; } = new();

    /// <summary>This client's attempt to join someone else's session.</summary>
    public JoinAttempt Join { get; } = new();

    /// <summary>Who may receive session state. See <see cref="SessionAudience"/> for the D-13 levels.</summary>
    public SessionAudience Audience { get; } = new();

    /// <summary>The requests waiting on the DM (R-1.3).</summary>
    public AdmissionDesk Admissions { get; } = new();

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
        Host.Start(SessionCodeGenerator.Next());
        SynchroniseTransport();
    }

    /// <summary>
    /// Ends the session. R-1.1 makes this the same path as closing or unloading the plugin, so the
    /// connection cannot outlive the session by taking a different exit.
    /// </summary>
    public void StopHosting()
    {
        Host.Stop();
        Audience.Clear();
        Admissions.Clear();
        Grace.Reset();
        JustLapsed = Array.Empty<PendingAdmission>();
        SynchroniseTransport();
    }

    /// <summary>Requests to join <paramref name="code"/>. A human action (R-1.3).</summary>
    public void RequestJoin(SessionCode code)
    {
        Join.Request(code);
        SynchroniseTransport();
    }

    /// <summary>Records that a participant is asking to be let in.</summary>
    public void ReceiveJoinRequest(PendingAdmission request) => Admissions.Receive(request);

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
        return Audience.Admit(peerCode, role, request?.Verification ?? AdmissionVerification.NotCompared);
    }

    /// <summary>
    /// Declines the pending participant. Nothing was ever addressable to them, so there is nothing
    /// to withdraw — which is the point of admitting rather than filtering (R-1.3, D-13).
    /// </summary>
    public void Deny(string peerCode)
    {
        Admissions.Decide(peerCode);
        Audience.Remove(peerCode);
    }

    /// <summary>Reports a transport failure against whichever side of the session is active.</summary>
    public void Fail(SessionFailure failure)
    {
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
        var wanted = Host.RequiresRelayConnection || JoinNeedsConnection();

        if (wanted && !_transport.IsConnected)
        {
            if (RelayEndpoint.TryParse(_relayAddress(), out var relay))
            {
                _transport.Connect(relay!);
            }
            else
            {
                Fail(SessionFailure.RelayUnreachable);
            }

            return;
        }

        if (!wanted && _transport.IsConnected)
        {
            _transport.Disconnect();
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
        JustLapsed = Admissions.ExpireLapsed(now);

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

    /// <summary>Unsubscribes from the transport. Wired into the plugin's teardown.</summary>
    public void Detach() => _transport.Failed -= OnTransportFailed;

    // Raised off the framework thread by the transport, so it is only recorded here and applied on
    // the next tick. Mutating session state from a socket callback would race the draw.
    private void OnTransportFailed(SessionFailure failure)
    {
        lock (_reportedFailureLock)
        {
            _reportedFailure = failure;
        }
    }

    private void ApplyReportedFailure()
    {
        SessionFailure failure;
        lock (_reportedFailureLock)
        {
            failure = _reportedFailure;
            _reportedFailure = SessionFailure.None;
        }

        if (failure != SessionFailure.None)
        {
            Fail(failure);
        }
    }

    private bool JoinNeedsConnection() =>
        Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;
}
