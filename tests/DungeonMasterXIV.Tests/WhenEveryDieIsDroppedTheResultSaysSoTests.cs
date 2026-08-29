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
}
