using System.Linq;
using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.2: every construct in R-2.1's grammar evaluates — pools, success counting against a typed
/// number, comparisons, keep/drop, exploding, rerolls, labelled terms, and nesting.
/// </summary>
/// <remarks>
/// <para>
/// <b>EACH CONSTRUCT IS A SEPARATE FAILURE, so each is a separate test.</b> The criterion says so in
/// those words. One omnibus test asserting "the grammar works" would go red as a single fact and
/// tell a reader nothing about WHICH construct broke — and would let seven constructs regress behind
/// the first one that fails.
/// </para>
/// <para>
/// Every expectation here is arithmetic the test did from faces the test scripted, never a second
/// answer from the evaluator (A-2.1's oracle rule, applied throughout rather than only where the
/// criterion names it).
/// </para>
/// </remarks>
public class EveryConstructInTheGrammarEvaluatesTests
{
    // POOLS.
    [Fact]
    public void APoolSumsItsDice()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(4, 4, 2, 6, 1)).Evaluate("5d6");

        Assert.Equal(4 + 4 + 2 + 6 + 1, outcome.Total);
    }

    // SUCCESS COUNTING against a number the USER TYPED (R-2.1). The result is a COUNT.
    [Fact]
    public void SuccessCountingCountsDiceBeatingATypedNumber()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(2, 8, 5, 9)).Evaluate("4d10>6");

        // 8 and 9 beat 6; 2 and 5 do not. The test counts them itself.
        Assert.Equal(2, outcome.Total);
    }

    // COMPARISONS, each operator its own case -- an operator table is where an off-by-one hides.
    [Theory]
    [InlineData("4d10>5", 2)]
    [InlineData("4d10>=5", 3)]
    [InlineData("4d10<5", 1)]
    [InlineData("4d10<=5", 2)]
    [InlineData("4d10=5", 1)]
    public void EachComparisonOperatorSelectsTheRightDice(string expression, int expected)
    {
        // faces 3, 5, 7, 9
        var outcome = new RollEvaluator(new ScriptedDieRoller(3, 5, 7, 9)).Evaluate(expression);

        Assert.Equal(expected, outcome.Total);
    }

    // KEEP/DROP -- the classic 4d6 drop lowest, and its three siblings.
    [Fact]
    public void KeepHighestKeepsTheBestDice()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate("4d6kh3");

        Assert.Equal(6 + 5 + 3, outcome.Total);
    }

    [Fact]
    public void KeepLowestKeepsTheWorstDice()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate("4d6kl2");

        Assert.Equal(1 + 3, outcome.Total);
    }

    [Fact]
    public void DroppedDiceAreStillReported()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate("4d6kh3");

        // A-2.1a one level down: the dropped die must be visible, or three dice and a total agree
        // with a roller that never rolled the fourth.
        Assert.Equal(4, outcome.Dice.Count);
        var dropped = Assert.Single(outcome.Dice, d => !d.Kept);
        Assert.Equal(1, dropped.Value);
    }

    // EXPLODING.
    [Fact]
    public void ABareExplodeRerollsOnTheMaximumFace()
    {
        // 6 explodes into 4; the 4 does not explode.
        var outcome = new RollEvaluator(new ScriptedDieRoller(6, 4)).Evaluate("1d6x");

        Assert.Equal(6 + 4, outcome.Total);
        Assert.Equal(2, outcome.Dice.Count);
    }

    [Fact]
    public void AnExplodeThresholdUsesTheTypedNumber()
    {
        // 9 meets >8 and explodes into 3; 3 does not.
        var outcome = new RollEvaluator(new ScriptedDieRoller(9, 3)).Evaluate("1d10x>8");

        Assert.Equal(9 + 3, outcome.Total);
    }

    // REROLLS.
    [Fact]
    public void ARerollReplacesTheMatchingDieAndShowsBoth()
    {
        // The 1 is rerolled into a 5.
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 5)).Evaluate("1d6r1");

        Assert.Equal(5, outcome.Total);
        Assert.Equal(2, outcome.Dice.Count);
        Assert.False(outcome.Dice[0].Kept);
        Assert.Equal(1, outcome.Dice[0].Value);
    }

    [Fact]
    public void ARerollHappensOncePerDieRatherThanUntilItStops()
    {
        // Two 1s in a row: the reroll is taken once, so the second 1 stands.
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 1)).Evaluate("1d6r1");

        Assert.Equal(1, outcome.Total);
    }

    // LABELLED TERMS.
    [Fact]
    public void ALabelledRollEvaluatesAndKeepsItsLabel()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(4, 4)).Evaluate("2d8+1 [damage]");

        Assert.Equal(4 + 4 + 1, outcome.Total);
        Assert.Equal("damage", outcome.Label);
    }

    // NESTING.
    [Fact]
    public void NestedParenthesesEvaluateInnermostFirst()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(2)).Evaluate("((1d4+1)*2)+3");

        Assert.Equal(((2 + 1) * 2) + 3, outcome.Total);
    }

    // COMBINED -- constructs on one term are a set, not a pipeline, so writing order must not matter.
    [Theory]
    [InlineData("4d6kh3>3")]
    [InlineData("4d6>3kh3")]
    public void ModifierWritingOrderDoesNotChangeTheResult(string expression)
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate(expression);

        // Keeps 6, 5, 3; of those, 6 and 5 beat 3.
        Assert.Equal(2, outcome.Total);
    }
}
