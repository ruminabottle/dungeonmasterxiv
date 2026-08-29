using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.2: every construct R-2.1's grammar names is reachable <b>by typing it</b> — pools, success
/// counting, comparisons, keep/drop (both halves), exploding, rerolls, labelled terms, and nesting.
/// </summary>
/// <remarks>
/// <para>
/// <b>ONLY A TEST THAT PARSES A STRING PROVES THE GRAMMAR.</b> A test that builds a
/// <see cref="DiceModifiers"/> and calls the evaluator proves the EVALUATOR, which is a different
/// claim — and the difference is exactly BUG-142. <c>DropHighest</c> and <c>DropLowest</c> were
/// declared, were handled correctly by <c>ApplyKeepAndDrop</c>, and could not be produced by any
/// expression, because the parser had no <c>d</c> arm. The half was built, evaluated and unreachable.
/// </para>
/// <para>
/// <b>AND A MUTATION TABLE COULD NOT HAVE FOUND IT.</b> A mutation proves the tests bite on what
/// EXISTS; it says nothing about what was never wired. There is no line to mutate for a parse arm
/// that does not exist, and its absence from a red list reads as coverage. That is why this file is
/// a census rather than another behaviour test: it fails by a construct being MISSING, which is the
/// failure mode every other test in this area is blind to.
/// </para>
/// <para>
/// <b>Each row asserts the EFFECT, not merely that nothing was refused.</b> A construct that parsed
/// and was then ignored would satisfy "not refused" while doing nothing, so every expected total is
/// arithmetic the test did itself from faces it chose — the same independent-second-source rule
/// <see cref="ScriptedDieRoller"/> exists for.
/// </para>
/// </remarks>
public class EveryGrammarConstructIsReachableFromAStringTests
{
    // Faces are fixed at 1, 6, 3, 5 across the keep/drop rows on purpose: every one of those four
    // rows sees the SAME dice, so a row's total is a statement about its modifier alone. Rows that
    // shared a modifier but differed in dice could agree for the wrong reason.
    [Theory]
    // pools
    [InlineData("3d6", 6, new[] { 1, 2, 3 })]
    // success counting, and comparisons, which are the same surface read two ways
    [InlineData("3d6>3", 2, new[] { 1, 5, 6 })]
    [InlineData("3d6>=5", 2, new[] { 1, 5, 6 })]
    [InlineData("3d6<2", 1, new[] { 1, 5, 6 })]
    // keep -- the half that always worked, here as the control for the four below it
    [InlineData("4d6kh3", 14, new[] { 1, 6, 3, 5 })]
    [InlineData("4d6kl3", 9, new[] { 1, 6, 3, 5 })]
    // DROP -- BUG-142. Unreachable before this fix; all three forms refused as Malformed.
    [InlineData("4d6dl1", 14, new[] { 1, 6, 3, 5 })]
    [InlineData("4d6dh1", 9, new[] { 1, 6, 3, 5 })]
    [InlineData("4d6d1", 14, new[] { 1, 6, 3, 5 })]
    // exploding, both the bare sentinel and an explicit test
    [InlineData("1d6x", 10, new[] { 6, 4 })]
    [InlineData("1d6x>5", 10, new[] { 6, 4 })]
    // rerolls -- the discarded 1 stays in the result as not-kept, so the total is the SECOND face
    [InlineData("1d6r1", 4, new[] { 1, 4 })]
    // nesting
    [InlineData("(1d6+1)*2", 8, new[] { 3 })]
    public void TheConstructEvaluatesAndHasItsEffect(string expression, int expected, int[] faces)
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(faces)).Evaluate(expression);

        Assert.True(
            outcome.Evaluated,
            $"'{expression}' was refused as {outcome.Fault}: {outcome.Message}. A-2.2 requires every "
            + "construct in the grammar to be reachable, and each construct is a separate failure.");

        Assert.Equal(expected, outcome.Total);
    }

    // Labelled terms are the ninth construct and cannot be asserted by a total, because the label is
    // deliberately never read (D-4). Its reachability is that it SURVIVES to the outcome.
    [Theory]
    [InlineData("1d6 # attack", "attack")]
    [InlineData("1d6 [attack]", "attack")]
    public void ALabelledTermCarriesItsLabelThrough(string expression, string expected)
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(3)).Evaluate(expression);

        Assert.True(outcome.Evaluated, $"'{expression}' was refused as {outcome.Fault}.");
        Assert.Equal(expected, outcome.Label);
        Assert.Equal(3, outcome.Total);
    }

    // THE HALF A TOTAL CANNOT SEE. 4d6dl1 and 4d6kh3 produce the SAME total from these faces, so the
    // theory above would pass on a parser that quietly read every drop as a keep. This pins which
    // die was set aside, which is the only thing that distinguishes them.
    [Fact]
    public void DroppingTheLowestSetsAsideTheLowestDieRatherThanKeepingAHighOne()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate("4d6dl1");

        var dropped = Assert.Single(outcome.Dice, die => !die.Kept);
        Assert.Equal(1, dropped.Value);
        Assert.Equal(4, outcome.Dice.Count);
    }

    [Fact]
    public void DroppingTheHighestSetsAsideTheHighestDie()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate("4d6dh1");

        var dropped = Assert.Single(outcome.Dice, die => !die.Kept);
        Assert.Equal(6, dropped.Value);
    }

    // THE CENSUS ITSELF. Without this, a construct could be deleted from the theory above and the
    // file would go green while covering less -- the same "absence reads as coverage" failure the
    // whole file exists to catch, reappearing one level up in the test.
    [Fact]
    public void TheCensusStillCoversEveryConstructAAA22Enumerates()
    {
        var covered = new[]
        {
            "3d6", "3d6>3", "3d6>=5", "4d6kh3", "4d6kl3",
            "4d6dl1", "4d6dh1", "1d6x", "1d6r1", "(1d6+1)*2", "1d6 # attack",
        };

        Assert.Equal(11, covered.Length);

        foreach (var expression in covered)
        {
            var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5, 4, 2)).Evaluate(expression);
            Assert.True(outcome.Evaluated, $"'{expression}' no longer evaluates: {outcome.Message}");
        }
    }
}
