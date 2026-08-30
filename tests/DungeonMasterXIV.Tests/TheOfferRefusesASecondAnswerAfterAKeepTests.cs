using System;
using DungeonMasterXIV.Data;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-182: answering a <see cref="SessionLogOffer"/> a second time is refused <b>after a keep</b>,
/// not only after a decline.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE TYPE HAD TWO NOTIONS OF "DONE" AND THEY DISAGREED.</b> <c>Keep</c> moved
/// <c>Outcome</c> — so <c>IsOpen</c> went false — while deliberately leaving the log held, which is
/// what makes <c>LineCount</c> readable afterwards. But the only guard on <c>Keep</c> and
/// <c>Decline</c> was the one on the LOG, so after a keep both were still answerable: a following
/// <c>Decline</c> destroyed the kept log and rewrote the record to say the player had declined.
/// </para>
/// <para>
/// <b>WHY THE EXISTING ROW DID NOT CATCH IT.</b>
/// <c>TheSessionEndOfferHoldsOneLogUntilItResolvesTests.AResolvedOfferCannotBeAnsweredASecondTime</c>
/// is named for the general property and exercises only the DECLINE-first orderings — the ones where
/// resolving nulls the log, so the log guard happens to answer. <b>Every keep-first ordering was
/// uncovered, and the name read as though they were not.</b> These cases are keep-first for that
/// reason.
/// </para>
/// <para>
/// <b>THE TWO REFUSALS SAY DIFFERENT THINGS, AND THAT IS ASSERTED RATHER THAN ASSUMED.</b> Both
/// throw <see cref="InvalidOperationException"/>, so the exception TYPE cannot tell them apart and a
/// test that checked only the type would pass against the wrong guard firing. Each case below pins
/// the MESSAGE: <i>already answered</i> is the resolve-once guard, <i>the log is gone</i> is the
/// older guard on the log itself.
/// </para>
/// </remarks>
public class TheOfferRefusesASecondAnswerAfterAKeepTests
{
    private const long Closes = 1_000;
    private static readonly Guid Campaign = Guid.NewGuid();

    // ---- THE FINDING. A keep must not be reversible into a decline.

    [Fact]
    public void DecliningAfterKeepingIsRefusedAndTheKeptLogIsStillIntact()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));
        offer.Keep();

        var refused = Assert.Throws<InvalidOperationException>(offer.Decline);

        // WHICH guard refused, not merely that one did.
        Assert.Contains("already been answered", refused.Message, StringComparison.OrdinalIgnoreCase);

        // THE HALF AN OUTCOME-ONLY ASSERTION CANNOT SEE: the kept log is still there. Without these
        // two lines a build that threw AFTER nulling the log would pass the line above.
        Assert.Equal(SessionLogOfferOutcome.Kept, offer.Outcome);
        Assert.Equal(1, offer.LineCount);
    }

    [Fact]
    public void KeepingTwiceIsRefusedRatherThanHandingTheLogOutAgain()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));
        offer.Keep();

        var refused = Assert.Throws<InvalidOperationException>(() => offer.Keep());

        Assert.Contains("already been answered", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SessionLogOfferOutcome.Kept, offer.Outcome);
        Assert.Equal(1, offer.LineCount);
    }

    // ---- THE DECLINE-FIRST ORDERING, WHICH ALREADY THREW. What is new is WHICH guard answers:
    //      the resolve-once one, reached before the log is consulted at all. Pinning that is what
    //      stops a later author "simplifying" the new guard back into a null-log check, which would
    //      restore the finding above while leaving this case green.

    [Fact]
    public void DecliningTwiceIsRefusedByTheResolveOnceGuardAndNotByTheMissingLog()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));
        offer.Decline();

        var refused = Assert.Throws<InvalidOperationException>(offer.Decline);

        Assert.Contains("already been answered", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("the log is gone", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- THE CONTROL, AND IT IS THE ONE THAT MUST NOT BECOME SYMMETRIC.
    //      ElapseTo is a POLL, not a command: callers ask it every frame. It answers false on an
    //      already-resolved offer and must keep answering rather than start throwing, or the fix
    //      above turns every frame after a keep into an exception.

    [Fact]
    public void ElapsingAfterTheChoiceResolvedStillAnswersFalseAndDoesNotThrow()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));
        offer.Keep();

        Assert.False(offer.ElapseTo(Closes + 1_000));

        Assert.Equal(SessionLogOfferOutcome.Kept, offer.Outcome);
        Assert.Equal(1, offer.LineCount);
    }

    [Fact]
    public void ElapsingAfterADeclineAlsoStillAnswersFalse()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));
        offer.Decline();

        Assert.False(offer.ElapseTo(Closes + 1_000));

        Assert.Equal(SessionLogOfferOutcome.Declined, offer.Outcome);
    }

    private static LoggedEntry Entry(int sequence, string peer, string text) =>
        new(new LoggedStamp(sequence, 100 + sequence), "message", peer, text);

    private static SessionLogOffer OfferOver(params LoggedEntry[] entries) =>
        new(new RetainedLog(Campaign, 500, entries), Closes);
}
