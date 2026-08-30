using System;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.13a: a key derived part-way through a drain is carried forward to the frames that follow it
/// <b>in that same drain</b>, so an acceptance and the first payload arriving together still open.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CRITERION HAD TESTS AND THIS MECHANISM HAD NONE, which is what kept it invisible.</b>
/// A grep for A-1.13a in the suite returns hits — roster ordering and sealing — and none of them is
/// about the carry-forward. qa-2's framing is the one to keep: its mutation showed the guard was
/// ABSENT, and the count of matching tests showed it LOOKED PRESENT. The second is what let the first
/// survive, on both sides of #207 at 1328/0 identically.
/// </para>
/// <para>
/// <b>Why the failure would be silent.</b> A payload that cannot be opened is discarded without a
/// word, and that is correct: keys are pairwise, the relay forwards every copy to every member, so a
/// client legitimately receives payloads it cannot open all the time. Treating one as an error would
/// make ordinary traffic look like an attack — which means the carry-forward failing produces an
/// EMPTY ROSTER and no other trace.
/// </para>
/// <para>
/// <b>The two frames must land in ONE drain, and that is the whole test.</b> Nothing is ticked
/// between them. On a later tick the coordinator passes the membership key in as
/// <c>OpenWith</c> and the payload opens without any carry-forward at all — so a test that ticked
/// between the two would pass against a build with the carry-forward removed. That case is below as
/// the control, precisely because it is the one that proves nothing on its own.
/// </para>
/// </remarks>
public class TheKeyDerivedInThisDrainOpensTheSameBatchTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    private static readonly DateTimeOffset Now = new(2026, 8, 30, 0, 30, 0, TimeSpan.Zero);

    /// <summary>A peer code the encoder could actually produce, derived rather than typed (BUG-57).</summary>
    private static readonly string Usable = SpeakableAlphabet.Characters[^SessionCode.Length..];

    // THE CRITERION. Acceptance and payload in ONE batch, ONE tick, ONE drain.
    //
    // ORDERED SO THAT ONLY THE LAST ASSERTION CAN FAIL under the mutation: admission itself does not
    // depend on the carry-forward, so the phase check passes either way and a red here is this
    // assertion rather than a collapse earlier in the test. Read the message, not the colour.
    [Fact]
    public void APayloadArrivingWithItsOwnAcceptanceIsOpened()
    {
        var (coordinator, transport, host) = JoinerAwaitingADecision();
        var key = host.DeriveSharedKey(coordinator.Membership.Keys!.PublicKey, Code);

        transport.Deliver(WireEnvelope.ForJoinAccepted(
            Code, coordinator.Membership.Keys!.PublicKey, host.PublicKey));
        Deliver(transport, key, $$"""{ "Roster": [ { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }""");

        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
        Assert.NotEmpty(coordinator.Roster);
    }

    // THE PREMISE, and without it the criterion could pass for the wrong reason. The carry-forward is
    // the ONLY key available to that payload only if the coordinator held no key when the tick began
    // -- otherwise OpenWith would supply one and the batching would be irrelevant.
    [Fact]
    public void TheCoordinatorHeldNoKeyWhenTheBatchArrived()
    {
        var (coordinator, transport, host) = JoinerAwaitingADecision();

        Assert.Null(coordinator.Membership.SessionKey);

        var key = host.DeriveSharedKey(coordinator.Membership.Keys!.PublicKey, Code);
        transport.Deliver(WireEnvelope.ForJoinAccepted(
            Code, coordinator.Membership.Keys!.PublicKey, host.PublicKey));
        Deliver(transport, key, $$"""{ "Roster": [ { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }""");
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(key, coordinator.Membership.SessionKey);
    }

    // THE CONTROL, AND IT IS THE ONE THAT SAYS WHAT THE CRITERION IS ABOUT. The same two envelopes
    // with a tick between them open through OpenWith and need no carry-forward, so this passes with
    // the mechanism removed. That is not a weakness in it -- it is the point. Without this row, a
    // red above could equally mean "payloads never open", and the file would be a test of sealing
    // rather than a test of batching.
    [Fact]
    public void TheSamePayloadInASEPARATEDrainOpensWithoutTheCarryForward()
    {
        var (coordinator, transport, host) = JoinerAwaitingADecision();
        var key = host.DeriveSharedKey(coordinator.Membership.Keys!.PublicKey, Code);

        transport.Deliver(WireEnvelope.ForJoinAccepted(
            Code, coordinator.Membership.Keys!.PublicKey, host.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(key, coordinator.Membership.SessionKey);

        Deliver(transport, key, $$"""{ "Roster": [ { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }""");
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.NotEmpty(coordinator.Roster);
    }

    // AND THE ORDER WITHIN THE BATCH IS THE DEPENDENCY. A payload arriving BEFORE its acceptance
    // cannot be opened by a key that does not exist yet -- it is dropped in silence, which is
    // correct. Pinned so that "the batch opened" is never mistaken for "order does not matter":
    // the criterion is that a key travels FORWARD, not that a drain is order-free.
    [Fact]
    public void APayloadAheadOfItsAcceptanceIsNotOpened()
    {
        var (coordinator, transport, host) = JoinerAwaitingADecision();
        var key = host.DeriveSharedKey(coordinator.Membership.Keys!.PublicKey, Code);

        Deliver(transport, key, $$"""{ "Roster": [ { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }""");
        transport.Deliver(WireEnvelope.ForJoinAccepted(
            Code, coordinator.Membership.Keys!.PublicKey, host.PublicKey));

        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
        Assert.Empty(coordinator.Roster);
    }

    /// <summary>Seals a document the way the host seals one for an admitted peer.</summary>
    private static void Deliver(FakeTransport transport, byte[] key, string json) =>
        transport.Deliver(WireEnvelope.ForSessionPayload(
            Code,
            SessionCipher.Seal(
                key,
                Encoding.UTF8.GetBytes(json),
                WireEnvelope.AssociatedDataFor(Code, WireMessageType.SessionPayload))));

    /// <summary>A joiner that has asked to join and has not yet been told, holding no session key.</summary>
    private static (SessionCoordinator Coordinator, FakeTransport Transport, SessionKeyExchange Host)
        JoinerAwaitingADecision()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance,
            capabilities: SessionCapabilities.Default);

        coordinator.RequestJoin(Code);
        coordinator.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        return (coordinator, transport, new SessionKeyExchange());
    }

    private sealed class FakeTransport : ISessionTransport
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
