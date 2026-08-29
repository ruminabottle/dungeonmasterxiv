using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.3c — a modifier is not bound to its term across <b>a space</b> (R-2.1, DMXENG-93).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CRITERION FORBIDS ONE PARSE AND DOES NOT NAME THE RIGHT ONE, AND THESE TESTS KEEP THAT
/// SHAPE.</b> <c>2d6 d20</c> must not read as <c>2d6</c> with a drop modifier. It does <i>not</i>
/// follow that it must be refused: a refusal satisfies A-2.3c and so does a two-term reading. So
/// every assertion here is <b>negative on the binding</b> and silent on the outcome — asserting what
/// the input evaluates AS would pin something nobody established.
/// </para>
/// <para>
/// <b>THE ROW GOVERNS EVERY MODIFIER, NOT ONLY THE DROP THAT EXPOSED IT.</b> The evidence is
/// Foundry's <c>MODIFIERS_REGEXP_STRING</c>, which is a NEGATED CHARACTER CLASS excluding the space
/// — so no modifier can contain one: <c>k</c>, <c>d</c>, <c>r</c>, <c>x</c> and a bare comparison
/// alike. The population below is the parser's whole modifier set, taken from
/// <c>RollDiceParser.ParseOne</c> rather than from the one case in the bug report.
/// </para>
/// <para>
/// <b>Each row is paired with its adjacent twin, and that pairing is the test.</b> A guard that only
/// checks the spaced form passes against a parser that has stopped binding modifiers altogether —
/// <c>2d6k1</c> must still bind, or the fix has removed the feature rather than scoped it. If a row
/// behaves the same with the space and without it, this file is not testing binding.
/// </para>
/// <para>
/// <b>Evidence class, carried deliberately:</b> the criterion rests on Foundry's published API
/// reference, <b>documentation of the implementation, UNRUN</b> — there is no Foundry on this machine.
/// Established by feature-engineer-2 across the v10 and v14 pages, four major versions apart, and
/// re-fetched independently for this chunk. <b>It cannot later be cited as if someone had watched
/// Foundry do it.</b>
/// </para>
/// </remarks>
public class AModifierDoesNotBindAcrossASpaceTests
{
    // THE PARSER'S WHOLE MODIFIER SET, not a sample: keep, drop, reroll, explode, count-successes.
    // Taken from RollDiceParser.ParseOne so a modifier added later without a row here shows up as a
    // gap in this census rather than as silence.
    [Theory]
    [InlineData("k1")]
    [InlineData("d1")]
    [InlineData("r1")]
    [InlineData("x")]
    [InlineData(">4")]
    public void NoModifierBindsToATermAcrossASpace(string modifier)
    {
        var parsed = RollParser.Parse($"2d6 {modifier}", RollLimits.Default);

        Assert.False(
            AnyTermCarriesAModifier(parsed),
            $"'2d6 {modifier}' bound the modifier across a space. A-2.3c forbids that parse. It does "
            + "NOT require a refusal — a two-term reading is equally acceptable — so the fix is to "
            + "stop the modifier binding, not to reject the input.");
    }

    // THE DISCRIMINATOR, AND WITHOUT IT EVERY ROW ABOVE PASSES AGAINST A PARSER THAT BINDS NOTHING.
    // Same modifiers, no space: these MUST bind. This is what makes the file a test of whitespace
    // rather than a test that modifiers are broken.
    [Theory]
    [InlineData("k1")]
    [InlineData("d1")]
    [InlineData("r1")]
    [InlineData("x")]
    [InlineData(">4")]
    public void TheSameModifierStillBindsWhenItIsAdjacent(string modifier)
    {
        var parsed = RollParser.Parse($"2d6{modifier}", RollLimits.Default);

        Assert.True(
            AnyTermCarriesAModifier(parsed),
            $"'2d6{modifier}' did not bind its modifier. The whitespace rule has removed the feature "
            + "rather than scoping it.");
    }

    // >>> UNRULED, AND PINNED RATHER THAN CLAIMED. This row records a CHOICE, not a requirement. <<<
    //
    // WHAT THIS REPLACED, because the replacement is the point: the comment here used to read
    // "whitespace is whitespace: a tab separates a term from a suffix exactly as a space does",
    // offered as the REASON the code was right. That is a general rule the evidence does not
    // support, and a stated justification outranks a loose name -- a name can be read as shorthand,
    // a reason is what the next reader believes.
    //
    // WHAT THE EVIDENCE ACTUALLY SAYS. Foundry's MODIFIERS_REGEXP_STRING is a negated class that
    // excludes U+0020 SPECIFICALLY. Measured by feature-engineer-3 by executing the pattern, with a
    // control, and reproduced here as they recorded it:
    //
    //     source ([^ (){}[\]+\-*/]+)   accepts a letter, a digit and '>'; rejects ' ', '+' and '('
    //     space  U+0020  excluded: TRUE
    //     tab    U+0009  excluded: FALSE      newline, CR, NBSP: also FALSE
    //     'd 20'  -> modifier match 'd'       'd\t20' -> modifier match 'd\t20'
    //
    // So A-2.3c reaches U+0020 and is SILENT on the tab, and Foundry may well bind across one.
    // This build does not, because RollCursor.AtWhitespace asks char.IsWhiteSpace. THAT IS NOT
    // ESTABLISHED AS A DIVERGENCE -- only as not established as conformance, and it is taken at
    // exactly that strength.
    //
    // The behaviour is UNMOVED by DMXENG-96 on purpose: changing it would settle by implementation
    // the very question this ticket exists to keep open. If this row reddens, the tab question has
    // been ruled and this file follows the ruling -- it is not on its own a defect.
    [Fact]
    public void ATabAlsoEndsTheTerm_UNRULED_ThisBuildsChoiceNotARequirement()
    {
        Assert.False(AnyTermCarriesAModifier(RollParser.Parse("2d6\td1", RollLimits.Default)));
    }

    // A SPACE elsewhere is untouched, and this is the regression the change could most easily cause:
    // the cursor skips whitespace before EVERY read, and only the modifier loop was narrowed.
    // '2d6 + 3' is one expression and must stay one. Named for the character it actually parses --
    // the row says nothing about a tab around an operator, which nobody has tested or ruled.
    [Fact]
    public void ASpaceAroundAnOperatorIsStillFine()
    {
        var parsed = RollParser.Parse("2d6 + 3", RollLimits.Default);

        Assert.Equal(RollFault.None, parsed.Fault);
        Assert.IsType<BinaryNode>(parsed.Node);
    }

    // Whether the parse REFUSED or produced something, does any dice term in it carry a modifier?
    // A refusal carries no node and so binds nothing, which satisfies the criterion.
    private static bool AnyTermCarriesAModifier(RollParse parse) =>
        parse.Node is not null && CarriesAModifier(parse.Node);

    private static bool CarriesAModifier(RollNode node) => node switch
    {
        DiceNode dice => dice.Modifiers != DiceModifiers.None,
        BinaryNode binary => CarriesAModifier(binary.Left) || CarriesAModifier(binary.Right),
        NegateNode negate => CarriesAModifier(negate.Operand),
        _ => false,
    };
}
