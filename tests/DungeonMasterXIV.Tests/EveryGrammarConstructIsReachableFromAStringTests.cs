using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    // THE CENSUS, AND IT NOW READS THE THEORY RATHER THAN REPEATING IT (BUG-150). The previous
    // version listed the same eleven expressions a second time and claimed that deleting a row from
    // the theory could not pass silently. It could: a second copy of a list agrees with the first
    // only until one of them changes, and the only trace was the suite total dropping by one, which
    // is not something a reviewer sees on a diff.
    //
    // A GUARD OVER A DUPLICATED LIST IS ITSELF THE ARGUMENT FOR DERIVING IT. So the rows come from
    // the theory's own attributes, and each construct is a QUESTION ASKED OF THAT SET. Delete the
    // 4d6dl1 row and no row answers "drop lowest", so this reddens by name.
    [Fact]
    public void EveryConstructAAA22NamesIsStillCoveredByTheTheoryAbove()
    {
        var rows = TheoryExpressions().ToList();

        Assert.NotEmpty(rows);

        var required = new (string Construct, Func<string, bool> Covered)[]
        {
            ("a pool", e => e.Contains("d6", StringComparison.Ordinal)),
            ("success counting", e => e.Contains('>') || e.Contains('<')),
            ("keep highest", e => e.Contains("kh", StringComparison.Ordinal)),
            ("keep lowest", e => e.Contains("kl", StringComparison.Ordinal)),
            ("drop lowest", e => e.Contains("dl", StringComparison.Ordinal)),
            ("drop highest", e => e.Contains("dh", StringComparison.Ordinal)),
            ("exploding", e => e.Contains('x')),
            ("rerolls", e => e.Contains('r')),
            ("nesting", e => e.Contains('(')),
        };

        var uncovered = required.Where(r => !rows.Any(r.Covered)).Select(r => r.Construct).ToList();

        Assert.True(
            uncovered.Count is 0,
            $"No row of the theory covers: {string.Join(", ", uncovered)}. A-2.2 makes each construct "
            + "a separate failure, so a construct losing its row is a construct losing its guard.");
    }

    // The tenth construct, asked of its own theory for the same reason.
    [Fact]
    public void LabelledTermsAreStillCoveredByTheirOwnTheory()
    {
        var rows = ExpressionsOf(nameof(ALabelledTermCarriesItsLabelThrough)).ToList();

        Assert.Contains(rows, e => e.Contains('#'));
        Assert.Contains(rows, e => e.Contains('['));
    }

    // THE PREMISE THIS FILE'S CENSUS RESTS ON. Reflection that silently found nothing would make
    // every question above vacuously satisfiable -- an empty set has no uncovered constructs only
    // because it has no rows. Asserting the count separately is what stops "no rows" reading as
    // "all covered", which is this file's own subject applied to its own instrument.
    [Fact]
    public void TheReflectionActuallyReachesTheTheoryData()
    {
        Assert.Equal(13, TheoryExpressions().Count());
        Assert.Equal(2, ExpressionsOf(nameof(ALabelledTermCarriesItsLabelThrough)).Count());
    }

    private static IEnumerable<string> TheoryExpressions() =>
        ExpressionsOf(nameof(TheConstructEvaluatesAndHasItsEffect));

    private static IEnumerable<string> ExpressionsOf(string method) =>
        typeof(EveryGrammarConstructIsReachableFromAStringTests)
            .GetMethod(method)!
            .GetCustomAttributes<InlineDataAttribute>()
            .Select(row => (string)row.GetData(null!).Single()[0]!);
}
