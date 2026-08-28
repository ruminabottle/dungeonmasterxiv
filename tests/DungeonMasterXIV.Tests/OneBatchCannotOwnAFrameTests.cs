using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-58: no batch of inbound frames can own a whole game frame, and none is lost getting there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deferral, not refusal.</b> <c>Drain</c> took the entire queue in one <c>Tick</c>, so a stranger
/// could decide how much work the DM's client did in a single frame — the join path is open to
/// strangers by design, which is what makes it the one worth bounding. The fix defers; it turns
/// nobody away, which is why it implies no user-facing behaviour and needed no product decision.
/// </para>
/// <para>
/// <b>Everything here goes through the transport.</b> Poking the queue directly would prove the slice
/// works and nothing about the path a frame actually travels.
/// </para>
/// </remarks>
public class OneBatchCannotOwnAFrameTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 3, 0, 0, TimeSpan.Zero);

    // The bug. Fails against a Drain that takes the whole queue: every request lands in one Tick and
    // the DM's client wears the entire cost of whatever a stranger chose to send.
    [Fact]
    public void ALargeBatchIsSpreadAcrossTicksRatherThanOne()
    {
        var (coordinator, transport) = Hosting();
        var keys = Deliver(transport, coordinator, count: 40);

        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.InRange(coordinator.Admissions.Pending.Count, 1, keys.Count - 1);
    }

    // The other half, and the one a bound alone would fail: deferred is not dropped. Ticking until
    // the queue is empty must yield every request, once each, in the order they arrived.
    [Fact]
    public void EveryDeferredFrameStillArrivesAndInOrder()
    {
        var (coordinator, transport) = Hosting();
        var keys = Deliver(transport, coordinator, count: 40);

        for (var tick = 0; tick < 100 && coordinator.Admissions.Pending.Count < keys.Count; tick++)
        {
            coordinator.Tick(TimeSpan.Zero, Now);
        }

        Assert.Equal(keys.Count, coordinator.Admissions.Pending.Count);
        Assert.Equal(
            keys,
            coordinator.Admissions.Pending.Select(pending => pending.JoinerPublicKey!).ToList());
    }

    // THE POSITIVE HALF. A bound that quietly delays ordinary traffic is a worse bug than the one
    // being fixed, and this is the test that catches it: one joiner, admitted on the very next Tick,
    // no extra frame of latency. Mutating the bound to zero reddens exactly this.
    [Fact]
    public void AnOrdinaryJoinRequestIsStillProcessedOnTheNextTick()
    {
        var (coordinator, transport) = Hosting();
        var keys = Deliver(transport, coordinator, count: 1);

        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(keys[0], Assert.Single(coordinator.Admissions.Pending).JoinerPublicKey);
    }

    // A whole FFXIV party arriving in the same frame is the largest legitimate simultaneous join, and
    // the bound is sized so it is never deferred. Fails if the bound is set below a full party.
    [Fact]
    public void AFullPartyArrivingAtOnceIsNotDeferred()
    {
        var (coordinator, transport) = Hosting();
        var keys = Deliver(transport, coordinator, count: 8);

        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(keys.Count, coordinator.Admissions.Pending.Count);
    }

    private static List<byte[]> Deliver(FakeTransport transport, SessionCoordinator coordinator, int count)
    {
        var code = coordinator.Host.Code!.Value;
        var keys = new List<byte[]>();

        for (var i = 0; i < count; i++)
        {
            using var joiner = new SessionKeyExchange();
            keys.Add(joiner.PublicKey);
            transport.Deliver(WireEnvelope.ForJoinRequest(code, joiner.PublicKey));
        }

        return keys;
    }

    private static (SessionCoordinator Coordinator, FakeTransport Transport) Hosting()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);
        coordinator.StartHosting();
        coordinator.Host.Registered();
        return (coordinator, transport);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) { }

        /// <summary>Puts a real encoded frame on the wire, the way the relay would.</summary>
        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
