using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// BUG-88: an admission answer is forwarded to the joiner it names and to nobody else — the
/// confidentiality half of R-1.5c, at the one place the recipient list is chosen.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE HARNESS NEEDS A BYSTANDER, AND THAT IS THE WHOLE POINT OF THIS FILE.</b> An assertion on
/// <c>Recipients</c> already existed for an acceptance —
/// <c>ThePendingNoticeReachesTheJoinerTests.AnAcceptanceOnTheSamePathIsForwarded</c> asserts
/// <c>["joiner-1"]</c>. It did not catch the mutation that prompted this bug, and the reason is not
/// that it is a weak assertion: <b>its session contains exactly one joiner, so the correct
/// single-recipient list and a broadcast to the whole session are THE SAME LIST.</b> The gap was
/// never a missing assertion. It was an assertion with nothing to distinguish.
/// </para>
/// <para>
/// So every case here admits <c>joiner-1</c> first and then answers <c>joiner-2</c>. There is
/// somebody else in the room who must not receive, and the tests say so twice — once by pinning the
/// list exactly, and once by naming the bystander in a <c>DoesNotContain</c>, because the second
/// reads as the property and the first reads as an implementation detail.
/// </para>
/// <para>
/// <b>The mutation this exists to stop is a plausible one, not a contrived one.</b>
/// <see cref="SessionRegistry.MembersExcept"/> is the CORRECT routing for a payload — there is a test
/// called <c>PayloadRecipientsAreEveryoneButTheSender</c>. "Forward the acceptance to the session,
/// the way we do payloads" is a one-line refactor toward consistency, written by somebody being
/// tidy. Since DMXENG-47 an acceptance carries a <c>ParticipantId</c>, which is the relink claim, so
/// that refactor would hand every joiner's claim to every other joiner.
/// </para>
/// <para>
/// <b>All three arms, not just the reported one.</b> <c>JoinDenied</c> and <c>JoinLapsed</c> take the
/// same <c>RouteAdmission</c> path and had no recipient assertion of any kind. Guarding only the arm
/// that was reported is the denylist shape — on BUG-85 that turned out to be load-bearing for a
/// reason nobody had predicted, and the cost of covering the siblings here is three lines.
/// </para>
/// </remarks>
public sealed class AnAdmissionAnswerReachesOnlyItsJoinerTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly byte[] HostKey = [4, 5, 6];
    private static readonly byte[] FirstJoinerKey = [1, 2, 3];
    private static readonly byte[] SecondJoinerKey = [7, 8, 9];

    private readonly SessionRegistry _registry = new();
    private readonly RelayRouter _router;

    public AnAdmissionAnswerReachesOnlyItsJoinerTests() => _router = new RelayRouter(_registry);

    // THE VACUITY CONTROL, and it is the assertion the existing coverage was missing rather than a
    // formality. If this fails, every test below is comparing a one-element list against a one-element
    // list and cannot tell a targeted answer from a broadcast -- which is exactly how the gap survived
    // having an assertion written over it.
    [Fact]
    public void ThereIsABystanderWhoWouldReceiveABroadcast()
    {
        ASessionWithAnAdmittedJoinerAndAWaitingOne();

        Assert.True(
            _registry.IsMember(Code.Value, "joiner-1"),
            "No bystander is admitted, so a broadcast and a targeted answer would produce the same "
            + "recipient list and these tests would pass against either.");

        Assert.Contains("joiner-1", _registry.MembersExcept(Code.Value, "host-1"));
    }

    // THE REPORTED ARM. Fails if: the acceptance is forwarded to session membership rather than to the
    // joiner it names -- qa-2's mutation, _registry.MembersExcept(code.Value, senderConnectionId).
    [Fact]
    public void AnAcceptanceReachesOnlyTheJoinerItNames()
    {
        ASessionWithAnAdmittedJoinerAndAWaitingOne();

        var decision = _router.Route(
            WireEnvelope.ForJoinAccepted(Code, SecondJoinerKey, HostKey), "host-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(RelayOutcome.JoinerAdmitted, decision.Outcome);
        Assert.Equal(["joiner-2"], decision.Recipients);

        // Said again as the property rather than as the list, because this is the sentence that
        // matters: since DMXENG-47 the acceptance carries joiner-2's ParticipantId, which is its
        // relink claim, and joiner-1 is a stranger who must never see it.
        Assert.DoesNotContain("joiner-1", decision.Recipients);
    }

    // A denial names one joiner and closes one connection. Fails if: a refusal is broadcast -- which
    // would tell the room that a particular key was refused, and under the same mutation would not
    // even reach the joiner it refused, since TryDeny removes the pending entry before it is a member.
    [Fact]
    public void ADenialReachesOnlyTheJoinerItNames()
    {
        ASessionWithAnAdmittedJoinerAndAWaitingOne();

        var decision = _router.Route(
            WireEnvelope.ForJoinDenied(Code, SecondJoinerKey), "host-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(["joiner-2"], decision.Recipients);
        Assert.DoesNotContain("joiner-1", decision.Recipients);
    }

    // Lapse is deliberately not denial -- nobody refused this player -- and it travels the same arm,
    // so it needs the same pin. Covered separately rather than folded in with the denial: a change
    // that collapsed one into the other would pass a shared assertion.
    [Fact]
    public void ALapseReachesOnlyTheJoinerItNames()
    {
        ASessionWithAnAdmittedJoinerAndAWaitingOne();

        var decision = _router.Route(
            WireEnvelope.ForJoinLapsed(Code, SecondJoinerKey), "host-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(["joiner-2"], decision.Recipients);
        Assert.DoesNotContain("joiner-1", decision.Recipients);
    }

    // POSITIVE CONTROL on the same registry, router and session: a payload IS routed to everyone but
    // its sender. Without this, a harness in which nothing was ever forwarded to more than one
    // connection would make all three tests above pass for the wrong reason -- and "the recipient list
    // is always one element here" is precisely the wrong reason available.
    [Fact]
    public void APayloadOnTheSamePathStillReachesTheWholeSession()
    {
        ASessionWithAnAdmittedJoinerAndAWaitingOne();

        _router.Route(WireEnvelope.ForJoinAccepted(Code, SecondJoinerKey, HostKey), "host-1");

        var decision = _router.Route(
            WireEnvelope.ForSessionPayload(Code, Sealed()), "joiner-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Contains("joiner-2", decision.Recipients);
        Assert.Contains("host-1", decision.Recipients);
    }

    /// <summary>
    /// <c>joiner-1</c> admitted and a member; <c>joiner-2</c> waiting. The bystander is what lets a
    /// broadcast be told apart from a targeted answer.
    /// </summary>
    private void ASessionWithAnAdmittedJoinerAndAWaitingOne()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");

        _router.Route(WireEnvelope.ForJoinRequest(Code, FirstJoinerKey), "joiner-1");
        _router.Route(WireEnvelope.ForJoinAccepted(Code, FirstJoinerKey, HostKey), "host-1");

        _router.Route(WireEnvelope.ForJoinRequest(Code, SecondJoinerKey), "joiner-2");
    }

    private static SealedPayload Sealed() =>
        SealedPayload.FromWire(new byte[12], [1, 2, 3, 4]);
}
