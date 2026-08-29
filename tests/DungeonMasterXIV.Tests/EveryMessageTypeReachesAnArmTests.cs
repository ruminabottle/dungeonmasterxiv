using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Every <see cref="WireMessageType"/> is either dispatched by <c>AdmissionInbox.Drain</c> or on an
/// explicit, reasoned exclusion list.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE THIRD HOP, AND THE ONLY ONE WITH NO SWEEP OVER IT.</b> A message crosses three: a client
/// sends it, the relay routes it, and the receiving client dispatches it.
/// <c>EveryMessageAClientSendsIsSentTests</c> covers the first and the relay's own table covers the
/// second. This covers the third — <b>and it is the hop carrying both of the incidents.</b> BUG-42
/// was a consumer nothing routed to; BUG-43 was a joiner's frame eaten by the host's arm.
/// </para>
/// <para>
/// <b>It exists because the gap it catches was found by READING.</b> BUG-75: the joiner sent the
/// comparability receipt, the relay routed it to the host, and <c>Drain</c> had no arm — so it
/// arrived and fell through to nothing, on <c>main</c>, with every test green. Nobody was going to
/// be told. That is the third failure of this shape here and the first two were also found by
/// somebody happening to look.
/// </para>
/// <para>
/// <b>Derived from the enum, not from a list.</b> A type added next month is covered without anyone
/// remembering to extend anything — the property A-1.12a asks of the send path, asserted at the
/// receiving end instead. <b>Adding a <see cref="WireMessageType"/> without a row here fails by
/// name.</b>
/// </para>
/// <para>
/// <b>What a green run does NOT mean.</b> It means every type is accounted for and every type
/// claimed to be dispatched reaches its handler. It does not mean the handler does the RIGHT thing —
/// that belongs to the tests named in each exclusion, and asserting it here would make this file a
/// second copy of the switch it is supposed to check.
/// </para>
/// </remarks>
public sealed class EveryMessageTypeReachesAnArmTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The state a receiving client must be in for the arm under test to be reachable.</summary>
    /// <remarks>
    /// <para>
    /// <b>Not a convenience — BUG-43 IS this distinction.</b> One socket carries both roles, and
    /// <see cref="WireMessageType.CodeRefused"/> means two different things depending on which side
    /// reads it: <i>your code is taken, pick another</i> to a <see cref="Registering"/> host, and
    /// <i>no session is live under that code</i> to a <see cref="Contacting"/> joiner. A probe
    /// arranged as both at once takes the host arm and the joiner never hears — which is the bug,
    /// not a limitation of the harness.
    /// </para>
    /// <para>
    /// <b><see cref="AwaitingDecision"/> is a real precondition, not a workaround.</b>
    /// <see cref="JoinAttempt.Admitted"/> refuses to fire from any other phase, because R-1.3a-i
    /// puts a pending notice before every decision so the joiner has the host key to compare
    /// against. Arranging it directly keeps THAT transition the subject of
    /// <see cref="WireMessageType.JoinPending"/>'s own row rather than a silent dependency of the
    /// four outcome rows below it.
    /// </para>
    /// </remarks>
    private enum Arrangement
    {
        Registering,
        Contacting,
        AwaitingDecision,
    }

    /// <summary>Why a type is not dispatched, or which handler proves that it is.</summary>
    private sealed record Arm(
        string? ExcludedBecause = null,
        Arrangement As = Arrangement.Registering,
        Func<Probe, bool>? Reached = null);

    // EVERY type, accounted for. A row is either an exclusion WITH A REASON or a handler that must
    // demonstrably fire. Nothing may be silently absent -- silent absence is BUG-75's whole shape.
    private static readonly Dictionary<WireMessageType, Arm> Expected = new()
    {
        [WireMessageType.Unknown] = new(
            ExcludedBecause: "the codec's name for a type this build does not recognise. D-14 makes "
            + "the wire format additive and requires a receiver to ignore what it cannot read, so "
            + "falling through IS the handling here rather than a gap in it."),

        [WireMessageType.CodeRequest] = new(
            ExcludedBecause: "a host sends this TO the relay and the relay answers it. A client "
            + "receiving one would be receiving another client's outbound traffic, which the relay "
            + "does not do -- so an arm for it would be dead code guarding an impossible frame."),

        [WireMessageType.CodeAccepted] = new(
            As: Arrangement.Registering, Reached: p => p.HostIsLive),

        [WireMessageType.CodeRefused] = new(
            As: Arrangement.Contacting, Reached: p => p.JoinFailedAsCodeNotActive),

        [WireMessageType.JoinRequest] = new(
            As: Arrangement.Registering, Reached: p => p.JoinRequestSeen),

        [WireMessageType.JoinerHoldsFingerprint] = new(
            As: Arrangement.Registering, Reached: p => p.ReceiptSeen),

        [WireMessageType.JoinPending] = new(
            As: Arrangement.Contacting, Reached: p => p.DecisionIsPending),

        [WireMessageType.JoinAccepted] = new(
            As: Arrangement.AwaitingDecision, Reached: p => p.JoinerWasAdmitted),

        [WireMessageType.JoinDenied] = new(
            As: Arrangement.AwaitingDecision, Reached: p => p.JoinerWasDenied),

        [WireMessageType.JoinLapsed] = new(
            As: Arrangement.AwaitingDecision, Reached: p => p.JoinerLapsed),

        [WireMessageType.SessionPayload] = new(
            ExcludedBecause: "dispatched, but only for a client holding a key that opens the seal, "
            + "and what the arm then does is decode content this file has no business asserting on. "
            + "Its arm is driven end to end by ASkippedParticipantIsReportedTests, which arranges a "
            + "real shared key; a bare frame here would prove only that an unopenable payload is "
            + "ignored, which is the SILENT path and would pass with the arm deleted."),
    };

    // THE UNIVERSAL, and the one that would have caught BUG-75 on the day the type was added. Fails
    // BY NAME on any value this file does not account for -- which is what a new message type looks
    // like before somebody wires its arm.
    [Fact]
    public void EveryMessageTypeIsAccountedFor()
    {
        var unaccounted = Enum.GetValues<WireMessageType>()
            .Where(type => !Expected.ContainsKey(type))
            .ToList();

        Assert.True(
            unaccounted.Count == 0,
            $"WireMessageType has {unaccounted.Count} value(s) this file does not account for: "
            + $"{string.Join(", ", unaccounted)}. Add a row saying either which handler receives it "
            + "or why nothing should. An unlisted type is one Drain may be dropping in silence.");
    }

    // A-1.12a's shape applied to the RECEIVING end: a type this file CLAIMS is dispatched must
    // actually reach its handler when a real frame arrives on the side that receives it. Fails if an
    // arm is deleted, reordered behind an earlier `continue`, or gated on a condition never met.
    [Theory]
    [MemberData(nameof(Dispatched))]
    public void ADispatchedTypeReachesItsHandler(WireMessageType type)
    {
        var arm = Expected[type];
        var probe = new Probe(arm.As);

        probe.Deliver(type);

        Assert.True(
            arm.Reached!(probe),
            $"A {type} frame reached Drain on a client that was {arm.As} and nothing consumed it. "
            + "That is "
            + "BUG-75's shape exactly: sent, routed, and silently dropped at the third hop.");
    }

    // THE CONTROL, and without it the Theory above is worth nothing. If Probe could not drive Drain
    // at all -- a frame the codec refuses, a queue never pumped, a phase that blocks every arm --
    // every row would report "not reached" and read as a wall of real defects; worse, somebody would
    // "fix" it by moving rows onto the exclusion list until the file checked nothing. This proves
    // the harness delivers, using the arm whose absence WAS BUG-42.
    [Fact]
    public void TheProbeCanActuallyDriveDrainSoAFailureMeansSomething()
    {
        var probe = new Probe(Arrangement.Registering);

        probe.Deliver(WireMessageType.JoinRequest);

        Assert.True(
            probe.JoinRequestSeen,
            "the harness cannot drive Drain at all, so no row in this file proves anything");
    }

    // Fails if: an exclusion is a shrug. Every excluded type carries an account a reader can
    // disagree with -- the point is that somebody DECIDED it should not be dispatched, not that the
    // list is short. An exclusion list nobody has to justify is how a sweep quietly stops sweeping.
    [Theory]
    [MemberData(nameof(Excluded))]
    public void AnExcludedTypeSaysWhy(WireMessageType type, string reason)
    {
        Assert.True(
            reason.Length > 80,
            $"{type} is excluded with a reason too short to be an account: \"{reason}\"");
    }

    public static TheoryData<WireMessageType> Dispatched()
    {
        var data = new TheoryData<WireMessageType>();
        foreach (var type in Expected.Where(e => e.Value.Reached is not null).Select(e => e.Key))
        {
            data.Add(type);
        }

        return data;
    }

    public static TheoryData<WireMessageType, string> Excluded()
    {
        var data = new TheoryData<WireMessageType, string>();
        foreach (var entry in Expected.Where(e => e.Value.ExcludedBecause is not null))
        {
            data.Add(entry.Key, entry.Value.ExcludedBecause!);
        }

        return data;
    }

    /// <summary>
    /// Drives the real <see cref="AdmissionInbox"/> from one side of the session and records what
    /// the frame did.
    /// </summary>
    private sealed class Probe
    {
        private readonly AdmissionInbox _inbox = new();
        private readonly JoinAttempt _attempt = new();
        private readonly HostSession _host = new();
        private readonly SessionKeyExchange _keys = new();
        private readonly SessionKeyExchange _other = new();

        public Probe(Arrangement arrangement)
        {
            // ONE side, never both. The untouched half stays in its resting phase so it cannot
            // swallow a frame meant for the other -- which is the collision BUG-43 was, and a probe
            // arranged as both at once would hide it here in the one file meant to catch it.
            switch (arrangement)
            {
                case Arrangement.Registering:
                    _host.Start(Code);
                    break;

                case Arrangement.Contacting:
                    _attempt.Request(Code);
                    break;

                case Arrangement.AwaitingDecision:
                    _attempt.Request(Code);
                    _attempt.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));
                    break;
            }
        }

        public bool JoinRequestSeen { get; private set; }

        public bool ReceiptSeen { get; private set; }

        public bool HostIsLive => _host.Phase == HostingPhase.Hosting;

        public bool JoinFailedAsCodeNotActive =>
            _attempt.Failure == SessionFailure.SessionCodeNotActive;

        public bool DecisionIsPending => _attempt.Phase == JoinPhase.AwaitingDecision;

        public bool JoinerWasAdmitted => _attempt.Phase == JoinPhase.Admitted;

        public bool JoinerWasDenied => _attempt.Phase == JoinPhase.Denied;

        public bool JoinerLapsed => _attempt.Phase == JoinPhase.Lapsed;

        public void Deliver(WireMessageType type)
        {
            _inbox.Receive(EnvelopeCodec.Encode(FrameOf(type, _keys, _other)));
            _inbox.Drain(
                _attempt,
                _keys,
                _host,
                new InboundHandlers(
                    OnJoinRequest: (_, _) => JoinRequestSeen = true,
                    OnComparabilityReceipt: _ => ReceiptSeen = true));
        }

        // Built through the REAL factories, so every frame this file delivers is one the product can
        // actually emit. A hand-rolled envelope would let a row pass against a shape nothing sends.
        private static WireEnvelope FrameOf(
            WireMessageType type,
            SessionKeyExchange mine,
            SessionKeyExchange theirs) => type switch
        {
            WireMessageType.CodeAccepted => WireEnvelope.ForCodeAccepted(Code),
            WireMessageType.CodeRefused => WireEnvelope.ForCodeRefused(Code),
            WireMessageType.JoinRequest => WireEnvelope.ForJoinRequest(Code, theirs.PublicKey),
            WireMessageType.JoinerHoldsFingerprint =>
                WireEnvelope.ForJoinerHoldsFingerprint(Code, theirs.PublicKey),
            WireMessageType.JoinPending =>
                WireEnvelope.ForJoinPending(
                    Code, mine.PublicKey, theirs.PublicKey, AdmissionDeadline.DecidedByHost(Now)),
            WireMessageType.JoinAccepted =>
                WireEnvelope.ForJoinAccepted(Code, mine.PublicKey, theirs.PublicKey),
            WireMessageType.JoinDenied => WireEnvelope.ForJoinDenied(Code, mine.PublicKey),
            WireMessageType.JoinLapsed => WireEnvelope.ForJoinLapsed(Code, mine.PublicKey),
            _ => throw new ArgumentOutOfRangeException(
                nameof(type), type, "no frame builder for a type this file claims is dispatched"),
        };
    }
}
