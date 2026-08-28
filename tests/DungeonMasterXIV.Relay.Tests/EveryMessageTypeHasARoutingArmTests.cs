using System;
using System.Linq;
using System.Text;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// Every message type this build knows must have an arm in <see cref="RelayRouter"/>. One that falls
/// through to <see cref="RelayOutcome.UnrecognisedMessageType"/> fails.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fourth instance of one defect, and the first the suite could not see.</b> BUG-40 was a
/// message with no sender; BUG-42 was one with no consumer; #86 was one with no arm in the client's
/// inbox. Each was caught inside the client. This one lives across the relay seam: the joiner sends
/// the receipt, the host has somewhere to put it, and every client-side test passes while the relay
/// silently drops it in between — which is precisely what happened to
/// <see cref="WireMessageType.JoinPending"/> and became BUG-33.
/// </para>
/// <para>
/// <b>Derived from the enum, not from a list of types.</b> There is no table here to fall out of
/// date: <see cref="Enum.GetValues{TEnum}"/> supplies the cases, and a type added next month is
/// covered without anyone remembering to extend anything. That is the same property A-1.12a asks of
/// the client's send path, asserted one layer out.
/// </para>
/// <para>
/// <b>And no factory is needed to reach it.</b> Frames are hand-built as JSON carrying the type
/// NUMBER, so a type with no <see cref="WireEnvelope"/> factory is still exercised. Building through
/// the factories would have made this a test of which factories exist, which is the enumeration it
/// is trying not to be.
/// </para>
/// </remarks>
public sealed class EveryMessageTypeHasARoutingArmTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    private readonly SessionRegistry _registry = new();
    private readonly RelayRouter _router;

    public EveryMessageTypeHasARoutingArmTests() => _router = new RelayRouter(_registry);

    // THE UNIVERSAL. Fails on any type that reaches the catch-all -- which is what a new client-sent
    // message looks like before somebody adds its arm, and what JoinerHoldsFingerprint would have
    // looked like the moment PR #88 merged.
    //
    // Asserts NOT-the-catch-all rather than "forwards", deliberately. Different types legitimately
    // drop for their own reasons -- a relay-only message from a client, an unknown joiner, a
    // malformed envelope -- and demanding a forward would force this test to encode each arm's
    // policy, which is the neighbouring tests' job. The defect signature is the FALL-THROUGH.
    [Theory]
    [MemberData(nameof(EveryTypeExceptUnknown))]
    public void EveryKnownMessageTypeIsRoutedByItsOwnArm(WireMessageType type)
    {
        AJoinerIsWaiting();

        var decision = _router.Route(FrameOfType(type), "joiner-1");

        Assert.NotEqual(RelayOutcome.UnrecognisedMessageType, decision.Outcome);
    }

    // THE CONTROL, and without it the test above is worth nothing. If Route never returned
    // UnrecognisedMessageType -- because the outcome was renamed, or the catch-all was removed --
    // every row above would pass while asserting nothing. This proves the condition is reachable.
    [Fact]
    public void ATypeThisBuildDoesNotKnowStillReachesTheCatchAll()
    {
        AJoinerIsWaiting();

        // 99 is not a defined WireMessageType, so EnvelopeCodec maps it to Unknown -- a message from
        // a newer client, which D-14 requires be tolerated rather than refused.
        var decision = _router.Route(FrameOfType((WireMessageType)99), "joiner-1");

        Assert.Equal(RelayOutcome.UnrecognisedMessageType, decision.Outcome);
        Assert.Equal(RelayAction.Drop, decision.Action);
    }

    // Unknown is excluded from the universal because the catch-all is its CORRECT destination, not a
    // gap: D-14 says a receiver ignores what it does not recognise, and this is the single place the
    // relay decides that. Asserted rather than left as a comment on the exclusion.
    [Fact]
    public void UnknownBelongsInTheCatchAllRatherThanNeedingAnArm()
    {
        AJoinerIsWaiting();

        var decision = _router.Route(FrameOfType(WireMessageType.Unknown), "joiner-1");

        Assert.Equal(RelayOutcome.UnrecognisedMessageType, decision.Outcome);
    }

    // The specific arm this chunk adds, asserted for what it does rather than only that it exists.
    // Fails if: the receipt is forwarded to the session at large -- who is joining and what their
    // client can do stays off every other member's wire (D-3, D-8).
    [Fact]
    public void AFingerprintReceiptGoesToTheHostAndNobodyElse()
    {
        AJoinerIsWaiting();

        var decision = _router.Route(
            WireEnvelope.ForJoinerHoldsFingerprint(Code, JoinerKey), "joiner-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(["host-1"], decision.Recipients);
    }

    // Fails if: the receipt moves the gate. The joiner is already pending and this answers nothing,
    // so a later acceptance must still find them -- the same property JoinPending needed, and the
    // reason both are their own arm rather than RouteAdmission with a flag.
    [Fact]
    public void AReceiptLeavesTheJoinerExactlyAsPending()
    {
        AJoinerIsWaiting();

        _router.Route(WireEnvelope.ForJoinerHoldsFingerprint(Code, JoinerKey), "joiner-1");

        Assert.False(_registry.IsMember(Code.Value, "joiner-1"));

        var acceptance = _router.Route(
            WireEnvelope.ForJoinAccepted(Code, JoinerKey, HostKey), "host-1");

        Assert.Equal(RelayOutcome.JoinerAdmitted, acceptance.Outcome);
    }

    // A host cannot report holding its own key, so anything sending this from the host's connection
    // is not the plugin. Fails if: the relay launders it onward.
    [Fact]
    public void AReceiptFromTheHostsOwnConnectionIsRefused()
    {
        AJoinerIsWaiting();

        var decision = _router.Route(
            WireEnvelope.ForJoinerHoldsFingerprint(Code, JoinerKey), "host-1");

        Assert.Equal(RelayAction.Drop, decision.Action);
        Assert.Equal(RelayOutcome.RelayOnlyMessageFromClient, decision.Outcome);
    }

    // The premise the rename rode on, measured rather than assumed: the wire carries the type as a
    // NUMBER, so renaming an enum member is a source change and not a wire change (D-14). If a
    // JsonStringEnumConverter were ever added, this fails and the rename becomes a breaking change.
    [Fact]
    public void TheWireCarriesTheTypeAsANumberSoRenamingAMemberIsNotAWireChange()
    {
        var json = Encoding.UTF8.GetString(
            EnvelopeCodec.Encode(WireEnvelope.ForJoinerHoldsFingerprint(Code, JoinerKey)));

        Assert.Contains($"\"Type\":{(int)WireMessageType.JoinerHoldsFingerprint}", json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(WireMessageType.JoinerHoldsFingerprint), json, StringComparison.Ordinal);
    }

    public static TheoryData<WireMessageType> EveryTypeExceptUnknown()
    {
        var data = new TheoryData<WireMessageType>();
        foreach (var type in Enum.GetValues<WireMessageType>().Where(t => t != WireMessageType.Unknown))
        {
            data.Add(type);
        }

        return data;
    }

    private static readonly byte[] JoinerKey = [1, 2, 3];
    private static readonly byte[] HostKey = [4, 5, 6];

    // Hand-built so any type can be reached, including one with no factory and one this build does
    // not define. The session code has to parse -- EnvelopeCodec refuses a malformed routing key,
    // which is deliberate and unrelated to the type.
    private static WireEnvelope FrameOfType(WireMessageType type)
    {
        var json = $$"""
            {"Type":{{(int)type}},"SessionCode":"{{Code.Value}}","PublicKey":"AQID"}
            """;

        Assert.True(
            EnvelopeCodec.TryDecode(Encoding.UTF8.GetBytes(json), out var envelope),
            $"The harness could not build a frame of type {(int)type}.");

        return envelope!;
    }

    private void AJoinerIsWaiting()
    {
        _router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");
        _router.Route(WireEnvelope.ForJoinRequest(Code, JoinerKey), "joiner-1");
    }
}
