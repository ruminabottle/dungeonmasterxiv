using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The comparability receipt, end to end: a frame arriving on the host's socket changes the pending
/// admission it names, and nothing else (R-1.3a-iv).
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this covers is one the completeness test cannot.</b>
/// <c>EveryMessageTypeReachesAnArmTests</c> proves <c>Drain</c> has an arm and that the arm calls
/// its handler — but it supplies that handler itself. <b>An arm firing into a handler nobody wired
/// is still a receipt that reaches nothing</b>, and that is exactly one edit away: deleting
/// <c>OnComparabilityReceipt:</c> from <see cref="SessionCoordinator"/>'s handler set leaves the
/// completeness test green. This drives the PRODUCTION wiring, from bytes on the transport to the
/// host's record.
/// </para>
/// <para>
/// <b>What the receipt means, stated because it is easy to over-read.</b> It reports that the
/// joining client HELD THE HOST KEY and could render a fingerprint — a capability, never a claim
/// that a human compared anything. R-1.3a-iii forbids the second: an acknowledgement of the human
/// act would ride the very channel an attacker controls, so it is forgeable exactly when it matters.
/// </para>
/// <para>
/// <b>And its ABSENCE means nothing.</b> A-1.2p admits fast, and qa-2 measured a 171ms admission
/// producing zero receipts from a joiner that could compare perfectly well. So the resting state is
/// <see cref="ComparabilityEvidence.NotEstablished"/> and the test below asserting that is not a
/// filler case — it is the state most real sessions decide in.
/// </para>
/// </remarks>
public sealed class TheReceiptReachesTheHostsRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // THE WHOLE HOP. Fails if: the arm is missing (BUG-75 as it stood), the handler is not wired
    // into SessionCoordinator, the lookup does not resolve the key to a peer code, or the record
    // does not take the state. Every one of those is a receipt that arrives and changes nothing.
    [Fact]
    public void AReceiptOnTheWireEstablishesThatJoinerCanCompare()
    {
        var (host, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        var pending = RequestJoin(host, transport, joiner);
        Assert.Equal(ComparabilityEvidence.NotEstablished, pending.Comparability);

        transport.Deliver(WireEnvelope.ForJoinerHoldsFingerprint(Code, joiner.PublicKey));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(ComparabilityEvidence.EstablishedCapable, pending.Comparability);
    }

    // THE NEGATIVE HALF, and the one that catches a lookup keyed on the wrong thing. A receipt names
    // ONE joiner. A version resolving to "the pending request" -- the first, the only, the most
    // recent -- would pass the test above and quietly mark a stranger comparable here, which is a
    // false qualification on the exact control R-1.3a exists to protect.
    //
    // THE SECOND JOINER SENDS IT, DELIBERATELY. My first version had the FIRST one send, and a probe
    // that replaced the lookup with `Pending.FirstOrDefault()` PASSED it -- the wrong answer and the
    // right answer are the same record when the receipt comes from the head of the list. Naming a
    // joiner who is not first is what makes the two distinguishable.
    [Fact]
    public void AReceiptFromOneJoinerLeavesTheOtherUntouched()
    {
        var (host, transport) = Hosting();
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();

        var firstPending = RequestJoin(host, transport, first);
        var secondPending = RequestJoin(host, transport, second);

        transport.Deliver(WireEnvelope.ForJoinerHoldsFingerprint(Code, second.PublicKey));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(ComparabilityEvidence.EstablishedCapable, secondPending.Comparability);
        Assert.Equal(ComparabilityEvidence.NotEstablished, firstPending.Comparability);
    }

    // A-1.2p's ordinary case, and it is the COMMON one rather than an edge. The DM clicks admit in a
    // second or two; the receipt is still in flight or was never sent. Fails if: anything infers
    // comparability from silence -- a timeout, an elapsed window, a default flipped for convenience.
    // "We waited and heard nothing" is NotEstablished held longer, never a transition.
    [Fact]
    public void WithoutAReceiptTheRecordStaysNotEstablishedHoweverLongItWaits()
    {
        var (host, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        var pending = RequestJoin(host, transport, joiner);

        for (var tick = 0; tick < 20; tick++)
        {
            host.Tick(TimeSpan.FromSeconds(30), Now.AddSeconds(30 * tick));
        }

        Assert.Equal(ComparabilityEvidence.NotEstablished, pending.Comparability);
    }

    // A receipt naming a key with no pending record. The joiner may have lapsed, been denied, or
    // never asked at all -- and the frame arrives from a stranger either way, because this path is
    // open to anyone who knows the code. Fails if: it throws, which would take the whole drain down
    // and with it every other client's traffic in the same batch.
    [Fact]
    public void AReceiptNamingNobodyIsIgnoredRatherThanThrowing()
    {
        var (host, transport) = Hosting();
        using var stranger = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinerHoldsFingerprint(Code, stranger.PublicKey));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Empty(host.Admissions.Pending);
    }

    // Drives the real JoinRequest path so the pending record is created the way production creates
    // it -- peer code derived from the key by the same call the receipt lookup will use later. Built
    // by hand, the two could disagree and every test above would still pass.
    private static PendingAdmission RequestJoin(
        SessionCoordinator host,
        DeliveringTransport transport,
        SessionKeyExchange joiner)
    {
        var before = host.Admissions.Pending.Select(p => p.PeerCode).ToList();

        transport.Deliver(WireEnvelope.ForJoinRequest(Code, joiner.PublicKey));
        host.Tick(TimeSpan.Zero, Now);

        var pending = host.Admissions.Pending.SingleOrDefault(p => !before.Contains(p.PeerCode));

        Assert.True(pending is not null, "the join request never became a pending admission");
        return pending!;
    }

    private static (SessionCoordinator Host, DeliveringTransport Transport) Hosting()
    {
        var transport = new DeliveringTransport();
        var host = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, new SilentLog(), SessionCapabilities.Default);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return (host, transport);
    }

    private sealed class DeliveringTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
        }

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
