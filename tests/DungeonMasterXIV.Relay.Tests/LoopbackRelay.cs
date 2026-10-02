using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Diagnostics;
using DungeonMasterXIV.Relay.Sessions;
using DungeonMasterXIV.Relay.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>Connects session coordinators through the real relay hub in memory and keeps every frame the relay received.</summary>
internal sealed class LoopbackRelay
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 20, 0, 0, TimeSpan.Zero);

    private readonly RelayHub _hub;
    private readonly ConnectionDirectory _directory = new();
    private readonly Dictionary<string, Client> _clients = new(StringComparer.Ordinal);
    private readonly List<SessionCoordinator> _coordinators = new();
    private readonly Queue<(Client From, byte[] Frame)> _inFlight = new();

    public LoopbackRelay()
    {
        var registry = new SessionRegistry();
        _hub = new RelayHub(new RelayRouter(registry), registry, _directory, new RelayLog(NullLogger<RelayLog>.Instance));
    }

    public List<byte[]> Seen { get; } = new();

    public SessionCoordinator Connect(string id, SessionCapabilities? capabilities = null)
    {
        var client = new Client(this, id);
        _clients[id] = client;

        var coordinator = new SessionCoordinator(
            client,
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            log: QuietLog.Instance,
            capabilities: capabilities ?? SessionCapabilities.Default);

        _coordinators.Add(coordinator);
        return coordinator;
    }

    public void Drop(string id) => _clients[id].Drop();

    public void RunUntil(Func<bool> condition, TimeSpan? step = null)
    {
        for (var round = 0; round < 200 && !condition(); round++)
        {
            foreach (var coordinator in _coordinators)
            {
                coordinator.Tick(step ?? TimeSpan.Zero, Now);
            }

            Deliver();
        }

        Assert.True(condition(), "The session never reached the expected state.");
    }

    private void Deliver()
    {
        while (_inFlight.TryDequeue(out var sent))
        {
            if (sent.From.RelaySide is not { } relaySide)
            {
                continue;
            }

            Seen.Add(sent.Frame);
            _hub.ReceiveAsync(relaySide, sent.Frame, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
    }

    private sealed class Client(LoopbackRelay relay, string name) : ISessionTransport
    {
        private int _dials;

        public RelaySide? RelaySide { get; private set; }

        public bool IsConnected => RelaySide is not null;

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relayAddress)
        {
            RelaySide = new RelaySide(this, $"{name}#{++_dials}");
            relay._directory.Add(RelaySide);
        }

        public void Disconnect() => Close(reportFailure: false);

        public void Send(byte[] envelope)
        {
            if (RelaySide is not null)
            {
                relay._inFlight.Enqueue((this, envelope));
            }
        }

        public void Drop() => Close(reportFailure: true);

        public void Deliver(byte[] frame) => Received?.Invoke(frame);

        public void Close(bool reportFailure)
        {
            if (RelaySide is not { } side)
            {
                return;
            }

            RelaySide = null;
            relay._hub.DisconnectAsync(side, "dropped", CancellationToken.None).AsTask().GetAwaiter().GetResult();
            if (reportFailure)
            {
                Failed?.Invoke(SessionFailure.ConnectionLost);
            }
        }
    }

    private sealed class RelaySide(Client client, string id) : IRelayConnection
    {
        public string Id { get; } = id;

        public ValueTask SendAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            client.Deliver(bytes);
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            client.Close(reportFailure: true);
            return ValueTask.CompletedTask;
        }
    }
}
