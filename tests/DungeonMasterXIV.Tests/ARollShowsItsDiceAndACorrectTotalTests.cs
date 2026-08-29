using System.Linq;
using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.1 and A-2.1a: <c>4d6+2</c> produces four individual die values in 1–6 and a correct total,
/// and <b>the total is verified against something that is NOT the roller.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CRITERION NAMES THE TRAP AND THESE TESTS ARE WRITTEN AROUND IT.</b> A-2.1: <i>the obvious
/// test asks the plugin for the dice and the total, both from the same computation</i> — so a roller
/// that sums wrongly produces a faithful log of a wrong number and passes.
/// </para>
/// <para>
/// Two independent sources are used here and neither is the evaluator's own total. The faces are
/// SCRIPTED, so the test knows them before it calls anything; and the assertion re-does the
/// arithmetic IN THE TEST from the dice the evaluator reports. An evaluator that sums wrongly fails
/// both, and an evaluator that reports dice inconsistent with its total fails the second.
/// </para>
/// </remarks>
public class ARollShowsItsDiceAndACorrectTotalTests
{
    [Fact]
    public void FourSixSidedDiceAndAModifierTotalWhatTheTestItselfComputes()
    {
        var roller = new ScriptedDieRoller(3, 6, 1, 4);
        var outcome = new RollEvaluator(roller).Evaluate("4d6+2");

        Assert.True(outcome.Evaluated);

        // The expected total is arithmetic the TEST did, from faces the TEST chose. It never asks
        // the evaluator what the dice were.
        Assert.Equal(3 + 6 + 1 + 4 + 2, outcome.Total);
    }

    [Fact]
    public void TheReportedDiceSumWithTheModifierToTheReportedTotal()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(3, 6, 1, 4)).Evaluate("4d6+2");

        // The second independent check: re-do the sum here, over what the evaluator reported.
        // An evaluator whose dice and total disagree cannot pass this even if both look plausible.
        var fromTheDice = outcome.Dice.Where(d => d.Kept).Sum(d => d.Value) + 2;

        Assert.Equal(outcome.Total, fromTheDice);
    }

    [Fact]
    public void EveryDieIsReportedIndividually()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(3, 6, 1, 4)).Evaluate("4d6+2");

        Assert.Equal(4, outcome.Dice.Count);
        Assert.Equal([3, 6, 1, 4], outcome.Dice.Select(d => d.Value));
    }

    // A-2.1a: showing the dice is a REQUIREMENT, not a display nicety -- a log of totals alone is
    // unfalsifiable by construction. This pins that the dice are exposed AT ALL, which is the thing
    // a future change could quietly drop while every total test kept passing.
    [Fact]
    public void ADiceRollExposesDiceRatherThanOnlyATotal()
    {
        var outcome = new RollEvaluator(new FixedDieRoller(5)).Evaluate("2d10");

        Assert.NotEmpty(outcome.Dice);
        Assert.All(outcome.Dice, die => Assert.Equal(10, die.Sides));
    }

    [Fact]
    public void EveryFaceLiesBetweenOneAndTheDieSize()
    {
        var outcome = new RollEvaluator(new SystemDieRoller()).Evaluate("20d6");

        Assert.All(outcome.Dice, die => Assert.InRange(die.Value, 1, 6));
    }

    // R-2.1: "d20 means 1d20".
    [Fact]
    public void ABareDMeansOneDie()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(17)).Evaluate("d20");

        Assert.True(outcome.Evaluated);
        Assert.Equal(17, outcome.Total);
        Assert.Single(outcome.Dice);
    }

    // R-2.1: an expression with no dice is still arithmetic, and reports no dice rather than fake ones.
    [Fact]
    public void ArithmeticWithoutDiceRollsNothing()
    {
        var roller = new ScriptedDieRoller();
        var outcome = new RollEvaluator(roller).Evaluate("2+3*4");

        Assert.Equal(14, outcome.Total);
        Assert.Empty(outcome.Dice);
        Assert.Equal(0, roller.Rolls);
    }

    // R-2.1: "(1d8+4)*2 is valid".
    [Fact]
    public void ParenthesesGroupBeforeMultiplication()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(3)).Evaluate("(1d8+4)*2");

        Assert.Equal((3 + 4) * 2, outcome.Total);
    }

    // D-4: a label is carried and never interpreted.
    [Fact]
    public void ALabelIsCarriedAndDoesNotChangeTheRoll()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(11)).Evaluate("1d20 [perception]");

        Assert.Equal("perception", outcome.Label);
        Assert.Equal(11, outcome.Total);
    }

    [Fact]
    public void AHashLabelIsCarriedToo()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(11)).Evaluate("1d20 #initiative");

        Assert.Equal("initiative", outcome.Label);
    }
}
