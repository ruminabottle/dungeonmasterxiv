using System;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class AdmissionDeadlineTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");
    private static readonly DateTimeOffset HostDecidedAt = new(2026, 8, 26, 20, 0, 0, TimeSpan.Zero);

    // The property R-1.3c actually needs, and the one a duration cannot provide. Two clients that
    // ask at different moments, with different local clocks, must agree on WHEN the window closes —
    // not on how long is left. Fails if: the deadline is ever re-derived from a duration on receipt.
    [Fact]
    public void EveryoneGivenTheSameDeadlineAgreesOnTheInstantHoweverLateTheyLook()
    {
        var deadline = AdmissionDeadline.DecidedByHost(HostDecidedAt);

        var asHostSeesIt = deadline.Instant;
        var asAJoinerSeesItTenMinutesLater = AdmissionDeadline.TryFromWire(deadline.UtcTicks)!.Value.Instant;

        Assert.Equal(asHostSeesIt, asAJoinerSeesItTenMinutesLater);
    }

    // Fails if: the deadline is decided anywhere but the host. There is no constructor taking a
    // TimeSpan, so a client cannot start its own clock — asserted here as the observable
    // consequence, that the value depends only on when the HOST decided.
    [Fact]
    public void TheDeadlineDependsOnlyOnWhenTheHostDecided()
    {
        var first = AdmissionDeadline.DecidedByHost(HostDecidedAt);
        var second = AdmissionDeadline.DecidedByHost(HostDecidedAt);

        Assert.Equal(first, second);
        Assert.Equal(HostDecidedAt.Add(AdmissionDeadline.Window), first.Instant);
    }

    // R-1.3c: the player sees the wait is bounded while it is happening. Fails if: remaining time
    // stops being computable from the instant alone.
    [Fact]
    public void RemainingTimeCountsDownTowardTheInstant()
    {
        var deadline = AdmissionDeadline.DecidedByHost(HostDecidedAt);

        Assert.Equal(AdmissionDeadline.Window, deadline.RemainingAt(HostDecidedAt));
        Assert.Equal(TimeSpan.FromMinutes(5), deadline.RemainingAt(HostDecidedAt.AddMinutes(10)));
    }

    // Fails if: a countdown can run negative, which would render as a growing negative number on
    // screen rather than as a lapse.
    [Fact]
    public void RemainingTimeIsFlooredAtZeroRatherThanGoingNegative()
    {
        var deadline = AdmissionDeadline.DecidedByHost(HostDecidedAt);

        Assert.Equal(TimeSpan.Zero, deadline.RemainingAt(HostDecidedAt.AddHours(3)));
        Assert.True(deadline.HasLapsedAt(HostDecidedAt.AddHours(3)));
        Assert.False(deadline.HasLapsedAt(HostDecidedAt.AddMinutes(14)));
    }

    // Fails if: the deadline is compared in local time. A DM in one timezone and a player in another
    // must reach the same answer, and a naive DateTime comparison would not.
    [Fact]
    public void TheDeadlineIsTimezoneIndependent()
    {
        var deadline = AdmissionDeadline.DecidedByHost(HostDecidedAt);
        var sameInstantElsewhere = HostDecidedAt.ToOffset(TimeSpan.FromHours(9)).AddMinutes(10);

        Assert.Equal(TimeSpan.FromMinutes(5), deadline.RemainingAt(sameInstantElsewhere));
    }

    // Fails if: the deadline stops surviving the wire, at which point the joiner has nothing to
    // count toward and has to invent a duration — the exact failure this type prevents.
    [Fact]
    public void TheDeadlineSurvivesTheWire()
    {
        using var joiner = new SessionKeyExchange();
        using var host = new SessionKeyExchange();
        var deadline = AdmissionDeadline.DecidedByHost(HostDecidedAt);

        // Built through ForJoinPending, the ONLY message that carries a deadline (DMXENG-41). It
        // used to be built through a ForJoinRequest overload that had no production caller -- so
        // this asserted the round trip over a shape nothing ever sent. Now it asserts it over the
        // shape that actually travels, which is what "survives the wire" was always meant to mean.
        var stamped = WireEnvelope.ForJoinPending(Code, joiner.PublicKey, host.PublicKey, deadline);

        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(stamped), out var received));

        Assert.Equal(deadline, received!.TryGetDeadline());
    }

    // THE BLOCKING FINDING FROM PR #10, and it is tested through the FULL DECODE PATH rather than
    // against the factory. Asserting TryFromWire in isolation proves the factory; the defect was
    // that a hostile envelope decoded SUCCESSFULLY and produced a deadline that threw when read, so
    // only bytes-in-to-countdown-out shows that stopped.
    //
    // Fails if: the range check is removed, or moved to a call site and left off this path. A relay
    // sending long.MaxValue used to decode cleanly, yield a non-null deadline, and throw from
    // Instant when RemainingAt read it — and R-1.3c puts that read in a draw path, so it was a
    // crash mid-frame from one hostile field.
    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    public void AHostileDeadlineDecodesToNothingRatherThanCrashingTheCountdown(long hostileTicks)
    {
        var hostile = Encoding.UTF8.GetBytes(
            $"{{\"Type\":4,\"SessionCode\":\"BKD7RM\",\"DeadlineUtcTicks\":{hostileTicks}}}");

        Assert.True(EnvelopeCodec.TryDecode(hostile, out var received));

        // Bound to a NON-NULLABLE local rather than repeating `!`. Reading these became extension
        // methods when WireEnvelopeReading was split out, so the receiver is now an ARGUMENT and its
        // nullability is checked at every call instead of flowing from the first `!` on the line.
        // That is the extension form surfacing something the instance form hid, so it is answered
        // once here rather than suppressed at each use.
        var arrived = Assert.IsType<WireEnvelope>(received);

        Assert.Null(arrived.TryGetDeadline());
        Assert.Null(Record.Exception(() => arrived.TryGetDeadline()));
    }

    // The other half of the same defect: a value inside the range must still work, or the fix would
    // have been "reject everything", which passes the test above and breaks the feature.
    [Fact]
    public void ADeadlineInsideTheRepresentableRangeStillArrives()
    {
        using var joiner = new SessionKeyExchange();
        using var host = new SessionKeyExchange();
        var deadline = AdmissionDeadline.DecidedByHost(HostDecidedAt);
        var wire = EnvelopeCodec.Encode(
            WireEnvelope.ForJoinPending(Code, joiner.PublicKey, host.PublicKey, deadline));

        Assert.True(EnvelopeCodec.TryDecode(wire, out var received));

        var arrived = Assert.IsType<WireEnvelope>(received);

        Assert.Equal(deadline, arrived.TryGetDeadline());
        Assert.Equal(AdmissionDeadline.Window, arrived.TryGetDeadline()!.Value.RemainingAt(HostDecidedAt));
    }

    // Fails if: R-1.3a and R-1.3c drift apart. They are the same window seen from two sides and
    // R-1.3a pairs the 15 minutes with the 11-character fingerprint explicitly — if the expiry ever
    // goes, the fingerprint must grow to 14 characters. This pins the half that lives in code.
    [Fact]
    public void TheWindowIsTheFifteenMinutesRule13aAndRule13cBothName()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), AdmissionDeadline.Window);
    }
}
