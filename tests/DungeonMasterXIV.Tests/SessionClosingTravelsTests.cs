using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3g / A-1.16: the DM's closing notice, and the countdown that makes it honest.
/// </summary>
public class SessionClosingTravelsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 1, 0, 0, TimeSpan.Zero);

    // >>> THE GUARD THAT MATTERS TO ANYONE ADDING A SECTION, NOT JUST TO ME.
    //
    // SessionContentCodec.Vetted REBUILDS the document — Roster is init-only, so it cannot edit in
    // place. A section added to SessionContent and forgotten in that rebuild is SILENTLY DROPPED ON
    // DECODE: the sender sets it, the wire carries it, the receiver never sees it, and nothing
    // fails anywhere. This is the only test that would notice.
    //
    // It is written against the CLOSING section because that is the one that exists today, and the
    // property it holds is general: vetting must not delete what it does not inspect.
    // THE ROSTER MUST BE NON-NULL OR THIS TEST PROVES NOTHING, and I shipped the vacuous version
    // first. Vetted returns the ORIGINAL document untouched when Roster is null, so a closing notice
    // sent without a roster never reaches the rebuild at all — the assertion passed with the field
    // deleted from the rebuild, which is the exact bug it exists to catch.
    //
    // Measured, not reasoned: with a null roster, deleting ClosingAtUtcTicks from the rebuild left
    // 9 passed. With a roster present it fails. A guard on a branch the test does not enter is not
    // a guard.
    [Fact]
    public void ASectionOtherThanTheRosterSurvivesVetting()
    {
        var closing = SessionClosing.DecidedByHost(Now.AddMinutes(5));
        var encoded = SessionContentCodec.Encode(new SessionContent
        {
            Roster = [new RosterEntry(PeerCodeThisProductGenerates, "Ysera", SessionRole.Player)],
            ClosingAtUtcTicks = closing.UtcTicks,
        });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));
        Assert.Equal(closing.UtcTicks, decoded!.ClosingAtUtcTicks);
        Assert.Single(decoded.Roster!);
    }

    /// <summary>A peer code of the shape this product actually emits, so Vetted keeps the entry.</summary>
    /// <remarks>
    /// Built from the alphabet and length the codec validates against rather than typed, so it
    /// cannot become impossible if either moves — the fixture mistake BUG-57 already found once.
    /// </remarks>
    private static readonly string PeerCodeThisProductGenerates =
        SpeakableAlphabet.Characters[^SessionCode.Length..];

    // A-1.16's second half, which R-1.3g calls a requirement rather than a courtesy: "closing" with
    // no remaining time is the indefinite wait R-1.3c and R-1.8 forbid. So the notice cannot be sent
    // without the instant that makes a countdown possible.
    [Fact]
    public void AClosingNoticeCarriesWhenItCloses()
    {
        var closing = SessionClosing.DecidedByHost(Now.AddMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(5), closing.RemainingAt(Now));
        Assert.False(closing.HasClosedAt(Now));
    }

    // An INSTANT, not a duration, and this is the test that says why. A duration is decided at
    // SEND and read at RECEIPT; those differ by the network, by clock skew, and by a suspended
    // client. Two receivers reading the same notice at different moments must see different
    // remaining times — if they saw the same, the value would be a duration wearing an instant's
    // name and R-1.3c's drift would be back.
    [Fact]
    public void TwoReadersAtDifferentMomentsSeeDifferentRemainingTime()
    {
        var closing = SessionClosing.DecidedByHost(Now.AddMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(5), closing.RemainingAt(Now));
        Assert.Equal(TimeSpan.FromMinutes(3), closing.RemainingAt(Now.AddMinutes(2)));
    }

    // Floored at zero. A countdown that runs negative is a countdown a participant cannot read, and
    // "closing in -00:14" is worse than no number.
    [Fact]
    public void TheCountdownNeverRunsNegative()
    {
        var closing = SessionClosing.DecidedByHost(Now);

        Assert.Equal(TimeSpan.Zero, closing.RemainingAt(Now.AddHours(1)));
        Assert.True(closing.HasClosedAt(Now.AddHours(1)));
    }

    // The value arrives from ANOTHER CLIENT and is rendered in a draw path, so an out-of-range
    // number is a crash rather than a bad countdown. There is no unvalidated construction path.
    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void AnImpossibleInstantFromTheWireIsRefusedRatherThanThrowing(long ticks)
    {
        Assert.Null(SessionClosing.TryFromWire(ticks));
    }

    [Fact]
    public void APossibleInstantFromTheWireRebuildsExactly()
    {
        var closing = SessionClosing.DecidedByHost(Now.AddMinutes(5));

        Assert.Equal(closing, SessionClosing.TryFromWire(closing.UtcTicks));
    }

    // A running session carries no closing notice at all, so "is it closing" is expressible as the
    // absence of a value rather than as a sentinel instant somebody has to recognise.
    [Fact]
    public void ARunningSessionCarriesNoClosingNotice()
    {
        var encoded = SessionContentCodec.Encode(new SessionContent { Roster = [] });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));
        Assert.Null(decoded!.ClosingAtUtcTicks);
    }
}
