using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-85 (D-11): an admission answer decides this client's attempt only if it is addressed to this
/// client.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every one of these tests asserts <see cref="JoinPhase.AwaitingDecision"/> before it delivers
/// anything</b>, and that is not ceremony. The first probe written for this bug passed clean and was
/// worthless: it never delivered a pending notice, so the attempt sat in
/// <see cref="JoinPhase.Contacting"/> and <c>JoinAttempt.Admitted()</c> returned on its own phase
/// guard. <b>A probe that cannot reach the defect is indistinguishable from a clean result</b>, so
/// the precondition is asserted rather than assumed.
/// </para>
/// <para>
/// <b>All three arms, because they are not equally exposed and the accept arm is the least so.</b>
/// <c>Admitted()</c> is guarded by its phase check, which accidentally narrows the accept arm to a
/// joiner already awaiting a decision. <c>Denied()</c> and <c>Lapsed()</c> have <b>no phase guard at
/// all</b> — they set the phase unconditionally. Fixing only the case that was reported would leave
/// the two arms that are easier to reach, which is the denylist-of-today's-cases shape
/// <c>AdmissionInbox</c> already argues against for BUG-56.
/// </para>
/// <para>
/// <b>Both directions.</b> A guard that refuses every answer passes all three negative tests and
/// breaks joining entirely, so each arm is paired with a positive that must still work.
/// </para>
/// <para>
/// <b>What is NOT claimed here:</b> this is not a confidentiality break. The victim can read nothing,
/// because a host never sealed anything for a client it never admitted. What breaks is that a client
/// reaches <see cref="JoinPhase.Admitted"/> with a derived key in a session nobody admitted it to.
/// </para>
/// </remarks>
public class AnAdmissionAnswerIsForItsAddresseeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 1, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // The vacuity control for every test below, stated once as its own fact. If this ever fails, the
    // negative tests are passing because nothing reached the code under test.
    [Fact]
    public void TheJoinerIsAwaitingADecisionBeforeAnyOutcomeIsDelivered()
    {
        var (coordinator, _, _) = Awaiting();

        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);
        Assert.Null(coordinator.Membership.SessionKey);
    }

    // THE REPORTED DEFECT. Fails if: an acceptance addressed to another joiner admits this one.
    [Fact]
    public void AnAcceptanceForSomebodyElseDoesNotAdmitThisClient()
    {
        var (coordinator, transport, host) = Awaiting();

        Deliver(coordinator, transport, WireEnvelope.ForJoinAccepted(Code, Stranger(), host.PublicKey));

        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);
        Assert.Null(coordinator.Membership.SessionKey);
    }

    // Denied() has NO phase guard, so this arm is reachable from any phase -- more exposed than the
    // acceptance above, not less. Fails if: a refusal meant for another joiner refuses this one.
    [Fact]
    public void ADenialForSomebodyElseDoesNotDenyThisClient()
    {
        var (coordinator, transport, _) = Awaiting();

        Deliver(coordinator, transport, WireEnvelope.ForJoinDenied(Code, Stranger()));

        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);
    }

    // Lapsed() likewise has no phase guard. Worth its own test rather than folding into the denial:
    // Lapsed and Denied are deliberately different states -- "nobody looked" versus "somebody
    // refused" -- and a fix that collapsed one into the other would pass a shared assertion.
    [Fact]
    public void ALapseForSomebodyElseDoesNotLapseThisClient()
    {
        var (coordinator, transport, _) = Awaiting();

        Deliver(coordinator, transport, WireEnvelope.ForJoinLapsed(Code, Stranger()));

        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);
    }

    // THE OTHER DIRECTION, and the half that a refuse-everything guard would fail. Each answer
    // addressed to this client must still decide its attempt.
    [Fact]
    public void AnAcceptanceAddressedToThisClientStillAdmits()
    {
        var (coordinator, transport, host) = Awaiting();

        Deliver(coordinator, transport, WireEnvelope.ForJoinAccepted(Code, Mine(coordinator), host.PublicKey));

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
        Assert.Equal(host.DeriveSharedKey(Mine(coordinator), Code), coordinator.Membership.SessionKey);
    }

    [Fact]
    public void ADenialAddressedToThisClientStillDenies()
    {
        var (coordinator, transport, _) = Awaiting();

        Deliver(coordinator, transport, WireEnvelope.ForJoinDenied(Code, Mine(coordinator)));

        Assert.Equal(JoinPhase.Denied, coordinator.Join.Phase);
    }

    [Fact]
    public void ALapseAddressedToThisClientStillLapses()
    {
        var (coordinator, transport, _) = Awaiting();

        Deliver(coordinator, transport, WireEnvelope.ForJoinLapsed(Code, Mine(coordinator)));

        Assert.Equal(JoinPhase.Lapsed, coordinator.Join.Phase);
    }

    /// <summary>
    /// A joiner that has requested, been told the DM is looking, and is genuinely awaiting a
    /// decision — reached by delivering a real pending notice rather than by calling
    /// <c>AwaitDecision</c> directly, so the phase is one the wire can actually produce.
    /// </summary>
    private static (SessionCoordinator Coordinator, FakeTransport Transport, SessionKeyExchange Host) Awaiting()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance, capabilities: SessionCapabilities.Default);
        var host = new SessionKeyExchange();

        coordinator.RequestJoin(Code);
        transport.Deliver(WireEnvelope.ForJoinPending(
            Code, coordinator.Membership.Keys!.PublicKey, host.PublicKey, AdmissionDeadline.DecidedByHost(Now)));
        coordinator.Tick(TimeSpan.Zero, Now);

        return (coordinator, transport, host);
    }

    private static void Deliver(SessionCoordinator coordinator, FakeTransport transport, WireEnvelope envelope)
    {
        transport.Deliver(envelope);
        coordinator.Tick(TimeSpan.Zero, Now);
    }

    /// <summary>This client's own key — the addressee an answer must name to be for it.</summary>
    private static byte[] Mine(SessionCoordinator coordinator) => coordinator.Membership.Keys!.PublicKey;

    /// <summary>Another joiner's key. A real one, so nothing passes because it was malformed.</summary>
    private static byte[] Stranger() => new SessionKeyExchange().PublicKey;

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = [];

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
