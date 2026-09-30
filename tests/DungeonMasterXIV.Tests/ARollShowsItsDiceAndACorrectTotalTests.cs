using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>A roll with scripted dice totals to arithmetic the test does itself, not the evaluator.</summary>
public class ARollShowsItsDiceAndACorrectTotalTests
{
    [Fact]
    public void FourSixSidedDiceAndAModifierTotalWhatTheTestItselfComputes()
    {
        var roller = new ScriptedDieRoller(3, 6, 1, 4);
        var outcome = new RollEvaluator(roller).Evaluate("4d6+2");

        Assert.True(outcome.Evaluated);
        Assert.Equal(3 + 6 + 1 + 4 + 2, outcome.Total);
    }
}
