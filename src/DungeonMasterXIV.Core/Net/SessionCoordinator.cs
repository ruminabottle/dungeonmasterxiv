using System;

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
    }

    /// <summary>The DM's hosting lifecycle.</summary>
    public HostSession Host { get; } = new();

    /// <summary>This client's attempt to join someone else's session.</summary>
    public JoinAttempt Join { get; } = new();

    /// <summary>Who may receive session state. See <see cref="SessionAudience"/> for the D-13 levels.</summary>
    public SessionAudience Audience { get; } = new();

    /// <summary>
    /// The session-scoped code of a participant awaiting a decision, or null. Never a character
    /// name — R-1.3 requires the prompt to identify a requester by code.
    /// </summary>
    public string? PendingRequestCode { get; private set; }

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
        PendingRequestCode = null;
        SynchroniseTransport();
    }

    /// <summary>Requests to join <paramref name="code"/>. A human action (R-1.3).</summary>
    public void RequestJoin(SessionCode code)
    {
        Join.Request(code);
        SynchroniseTransport();
    }

    /// <summary>Records that a participant is asking to be let in.</summary>
    public void ReceiveJoinRequest(string peerCode) => PendingRequestCode = peerCode;

    /// <summary>
    /// Admits the pending participant. Only after this does anything become addressable to them —
    /// see <see cref="SessionAudience"/>, which is where D-13's None level is enforced.
    /// </summary>
    public AdmittedPeer Admit(string peerCode)
    {
        PendingRequestCode = null;
        return Audience.Admit(peerCode);
    }

    /// <summary>
    /// Declines the pending participant. Nothing was ever addressable to them, so there is nothing
    /// to withdraw — which is the point of admitting rather than filtering (R-1.3, D-13).
    /// </summary>
    public void Deny(string peerCode)
    {
        PendingRequestCode = null;
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

    private bool JoinNeedsConnection() =>
        Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;
}
