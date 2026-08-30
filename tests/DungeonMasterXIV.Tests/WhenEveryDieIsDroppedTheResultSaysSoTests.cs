using System.Linq;
using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.3b: when an evaluation discards or drops <b>every</b> die, the result says so in words —
/// not only in markup.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE FAILS-CLAUSE IS SURVIVAL, NOT THE TOTAL, AND THE CRITERION SAYS SO AFTER STRIKING ITS OWN
/// NARROWER EXAMPLE.</b> A clause reading <i>"a build that returns a total of zero without stating
/// that nothing survived fails"</i> was struck within the hour: <c>4d6dl4+100</c> drops every die
/// and totals 100, so by the rule it must say so and by the struck clause it did not fail. Every
/// test here therefore asserts on the WORDS and none asserts <c>Total == 0</c>.
/// </para>
/// <para>
/// <b>TWO HALVES, AND THE SECOND IS THE ONE THAT IS EASY TO FAKE.</b> A build that always attached
/// the notice would pass any number of every-die-dropped cases. So the cases where a die DOES
/// survive, and the case where no die is rolled at all, are pinned just as hard — that is what gives
/// the assertion something to distinguish.
/// </para>
/// </remarks>
public class WhenEveryDieIsDroppedTheResultSaysSoTests
{
    private static RollOutcome Roll(string expression, params int[] faces) =>
        new RollEvaluator(new ScriptedDieRoller(faces)).Evaluate(expression);

    // ---- HALF ONE: nothing survived, so the result must say so, in words.

    [Fact]
    public void DroppingEveryDieIsStatedInWords()
    {
        var outcome = Roll("4d6dl4", 1, 6, 3, 5);

        Assert.True(outcome.Evaluated);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Notice));
        Assert.Contains("dropped", outcome.Notice, System.StringComparison.OrdinalIgnoreCase);
    }

    // THE CASE THE STRUCK CLAUSE MISSED, and the one a reader is least likely to notice because the
    // total looks entirely ordinary.
    [Fact]
    public void EveryDieDroppedIsStatedEvenWhenTheTotalLooksOrdinary()
    {
        var outcome = Roll("4d6dl4+100", 1, 6, 3, 5);

        Assert.Equal(100, outcome.Total);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Notice));
    }

    [Fact]
    public void KeepingNoneIsStated()
    {
        var outcome = Roll("4d6kh0", 1, 6, 3, 5);

        Assert.False(string.IsNullOrWhiteSpace(outcome.Notice));
    }

    // ---- HALF TWO: something survived, or nothing was rolled. NO statement.
    //      This half is what stops the notice being unfalsifiable.

    [Fact]
    public void KeepingEvenOneDieIsNotStated()
    {
        var outcome = Roll("4d6dl3", 1, 6, 3, 5);

        Assert.Null(outcome.Notice);
    }

    [Fact]
    public void AnOrdinaryRollIsNotStated()
    {
        var outcome = Roll("4d6+2", 1, 6, 3, 5);

        Assert.Null(outcome.Notice);
    }

    // ---- THE ZEROES THAT MUST STAY SILENT, and the direction nothing else here can measure.
    //      Both total ZERO with EVERY die KEPT, so A-2.3b's rule sentence does not engage: nothing
    //      was discarded, and there is nothing to say. The clause SQ-96 STRUCK -- "a build that
    //      returns a total of zero without stating that nothing survived fails" -- would announce
    //      that nothing survived on both of them, and that is WHY it was struck. These two tests
    //      are what let the suite tell the binding rule from the struck one.
    //      Until they existed it could not: every other silent case above has a NON-ZERO total, and
    //      the one zero-total case has no dice at all, so it slips under any condition guarded by
    //      dice.Count > 0. Measured, not argued -- reinstating the struck clause as a widening left
    //      the suite green.

    [Fact]
    public void CountingNoSuccessesIsAZeroWithEveryDieKept()
    {
        var outcome = Roll("4d10>9", 1, 6, 3, 5);

        // THE PREMISE FIRST. Without it this passes for an expression that was refused, or that
        // rolled no dice at all, and then it asserts nothing whatever about survival.
        Assert.Equal(0, outcome.Total);
        Assert.Equal(4, outcome.Dice.Count);
        Assert.All(outcome.Dice, die => Assert.True(die.Kept));

        Assert.Null(outcome.Notice);
    }

    [Fact]
    public void AZeroReachedByArithmeticKeepsEveryDieAndSaysNothing()
    {
        var outcome = Roll("4d6-15", 1, 6, 3, 5);

        Assert.Equal(0, outcome.Total);
        Assert.Equal(4, outcome.Dice.Count);
        Assert.All(outcome.Dice, die => Assert.True(die.Kept));

        Assert.Null(outcome.Notice);
    }

    // A DIE THAT WAS REROLLED AWAY IS NOT A DIE THAT DIED: its replacement survived.
    [Fact]
    public void ARerolledDieDoesNotCountAsNothingSurviving()
    {
        var outcome = Roll("1d6r1", 1, 5);

        Assert.Equal(5, outcome.Total);
        Assert.Null(outcome.Notice);
    }

    // AN EXPRESSION WITH NO DICE HAS NOTHING TO SURVIVE. This is the case that would trip a build
    // implementing "the total is zero" or "nothing was kept" without asking whether anything was
    // rolled -- 0+0 has no dice and a zero total, and must stay silent.
    [Fact]
    public void ArithmeticWithNoDiceIsNotStated()
    {
        var outcome = Roll("0+0");

        Assert.True(outcome.Evaluated);
        Assert.Equal(0, outcome.Total);
        Assert.Empty(outcome.Dice);
        Assert.Null(outcome.Notice);
    }

    // THE SAME ABSENCE WITH AN ORDINARY TOTAL. `0+0` above is the stronger control because it
    // carries the zero a total test keys on; this one separates a different wrong condition, "no
    // die was kept" taken alone, which is vacuously true of an empty dice list whatever the total.
    [Fact]
    public void ArithmeticWithNoDiceAndAnOrdinaryTotalIsNotStated()
    {
        var outcome = Roll("2+2");

        Assert.True(outcome.Evaluated);
        Assert.Equal(4, outcome.Total);
        Assert.Empty(outcome.Dice);
        Assert.Null(outcome.Notice);
    }

    [Fact]
    public void ARefusalCarriesNoSurvivalNotice()
    {
        var outcome = Roll("4d6dl4+");

        Assert.False(outcome.Evaluated);
        Assert.Null(outcome.Notice);
    }

    // The notice explains a result rather than replacing one: the dice and total are still there.
    [Fact]
    public void TheStatementAccompaniesTheResultRatherThanReplacingIt()
    {
        var outcome = Roll("4d6dl4", 1, 6, 3, 5);

        Assert.Equal(4, outcome.Dice.Count);
        Assert.All(outcome.Dice, die => Assert.False(die.Kept));
        Assert.NotNull(outcome.Notice);
    }

    // ---- A ROW WHOSE GRAMMAR IS NOT SETTLED, PINNED WITHOUT SETTLING IT.
    //      Whether a drop count larger than the pool clamps or refuses is an OPEN Spec Owner
    //      question (SQ-97), and A-2.3b is deliberately grammar-independent. So this asserts the
    //      criterion under BOTH rulings instead of choosing one, and it fails only in the state
    //      A-2.3b actually names: evaluated, dice rolled, none kept, and nothing said. On today's
    //      build the second arm is the live one -- it evaluates and every die is dropped.
    [Fact]
    public void DroppingMoreDiceThanWereRolledSaysSoIfItEvaluatesAtAll()
    {
        var outcome = Roll("4d6dl9", 1, 6, 3, 5);

        if (!outcome.Evaluated)
        {
            Assert.False(string.IsNullOrWhiteSpace(outcome.Message));
            Assert.Null(outcome.Notice);
        }
        else if (outcome.Dice.Any(die => die.Kept))
        {
            Assert.Null(outcome.Notice);
        }
        else
        {
            Assert.NotEmpty(outcome.Dice);
            Assert.False(string.IsNullOrWhiteSpace(outcome.Notice));
        }
    }
}
