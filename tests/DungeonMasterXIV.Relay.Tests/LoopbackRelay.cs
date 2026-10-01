using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>Connects session coordinators through the real relay router in memory and keeps every frame the relay saw.</summary>
internal sealed class LoopbackRelay
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 20, 0, 0, TimeSpan.Zero);

    private readonly RelayRouter _router = new(new SessionRegistry());
    private readonly Dictionary<string, Connection> _connections = new(StringComparer.Ordinal);
    private readonly List<SessionCoordinator> _coordinators = new();
    private readonly Queue<(string From, byte[] Frame)> _inFlight = new();

    public List<byte[]> Seen { get; } = new();

    public SessionCoordinator Connect(string id, SessionCapabilities? capabilities = null)
    {
        var connection = new Connection(this, id);
        _connections[id] = connection;

        var coordinator = new SessionCoordinator(
            connection,
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            log: QuietLog.Instance,
            capabilities: capabilities ?? SessionCapabilities.Default);

        _coordinators.Add(coordinator);
        return coordinator;
    }

    public void RunUntil(Func<bool> condition)
    {
        for (var round = 0; round < 50 && !condition(); round++)
        {
            foreach (var coordinator in _coordinators)
            {
                coordinator.Tick(TimeSpan.Zero, Now);
            }

            Deliver();
        }

        Assert.True(condition(), "The session never reached the expected state.");
    }

    private void Deliver()
    {
        while (_inFlight.TryDequeue(out var sent))
        {
            Seen.Add(sent.Frame);

            if (!EnvelopeCodec.TryDecode(sent.Frame, out var envelope) || envelope is null)
            {
                continue;
            }

            var decision = _router.Route(envelope, sent.From);

            if (decision.Action == RelayAction.ReplyToSender && decision.Reply is not null)
            {
                _connections[sent.From].Receive(EnvelopeCodec.Encode(decision.Reply));
            }
            else if (decision.Action == RelayAction.Forward)
            {
                foreach (var recipient in decision.Recipients)
                {
                    _connections[recipient].Receive(sent.Frame);
                }
            }
        }
    }

    private sealed class Connection(LoopbackRelay relay, string id) : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relayAddress) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => relay._inFlight.Enqueue((id, envelope));

        public void Receive(byte[] frame) => Received?.Invoke(frame);
    }
}
