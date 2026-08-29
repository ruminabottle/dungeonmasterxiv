using System.Linq;
using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Behaviour that is <b>UNRULED</b>, pinned exactly as it is, so that changing it later shows up as
/// a decision somebody made rather than as drift nobody attributed.
/// </summary>
/// <remarks>
/// <para>
/// <b>NOTHING HERE IS A REQUIREMENT, AND THAT IS THE MOST IMPORTANT SENTENCE IN THE FILE.</b> Every
/// assertion below records what the evaluator does today on a question no criterion answers. <b>A
/// test that asserts current behaviour without saying why READS AS A REQUIREMENT</b> to the next
/// person, who then "fixes" the code to keep it green — so each pin below states its own status, and
/// a red here is an invitation to check whether the change was intended, never on its own a defect.
/// </para>
/// <para>
/// <b>Why these three and not others.</b> DMXENG-93's fix stopped a modifier binding across
/// whitespace (A-2.3c). The scope fence around it said: fix the binding, not the clamping — so the
/// no-space forms had to stay exactly where they were. They did, and nothing pinned them, which
/// leaves the two expressions the open clamp question actually turns on unguarded.
/// </para>
/// <para>
/// <b>Measured on merged main at <c>c1d3092</c></b>, with DMXENG-93's fix present and no production
/// change of my own — because the point of a pin is what the shipped code does, and I had previously
/// only inferred the third case from reading a diff.
/// </para>
/// </remarks>
public class UnruledRollBehavioursPinnedAsFoundTests
{
    // PIN 1 — UNRULED: whether an over-large drop count CLAMPS or is REFUSED sits with the Spec
    // Owner. It clamps today. If this reddens, the clamp/refuse question has been answered and this
    // file should be updated to match the ruling, NOT the code adjusted to keep it green.
    //
    // ASSERTED ON THE COUNTS, NOT THE TOTAL. total=0 is reachable two ways -- dropping every die, or
    // rolling nothing at all -- so a total cannot tell the pinned behaviour from several others. The
    // discriminating facts are that TWO dice were rolled and NONE was kept.
    [Fact]
    public void ADropCountLargerThanThePoolClampsToDroppingAll_UNRULED()
    {
        var roller = new ScriptedDieRoller(1, 6);

        var outcome = new RollEvaluator(roller).Evaluate("2d6d20");

        Assert.True(outcome.Evaluated, $"'2d6d20' was refused as {outcome.Fault} — see the note above.");
        Assert.Equal(2, roller.Rolls);
        Assert.Equal(2, outcome.Dice.Count);
        Assert.DoesNotContain(outcome.Dice, die => die.Kept);
    }

    // PIN 2 — the same unruled question written the other way, with an explicit dl suffix rather
    // than a bare d. Both spellings reach the clamp, and a ruling that changed one without the other
    // would be a partial answer worth noticing.
    [Fact]
    public void AnExplicitDropLowestLargerThanThePoolAlsoClamps_UNRULED()
    {
        var roller = new ScriptedDieRoller(1, 6, 3, 5);

        var outcome = new RollEvaluator(roller).Evaluate("4d6dl9");

        Assert.True(outcome.Evaluated, $"'4d6dl9' was refused as {outcome.Fault} — see the note above.");
        Assert.Equal(4, roller.Rolls);
        Assert.Equal(4, outcome.Dice.Count);
        Assert.DoesNotContain(outcome.Dice, die => die.Kept);
    }

    // PIN 3 — UNRULED, AND NOT A DEFECT. A-2.3c stops a modifier binding across whitespace to its
    // TERM. Once the modifier LETTER is committed, whitespace inside the suffix still binds.
    //
    // THIS IS PINNED BECAUSE IT IS SURPRISING, NOT BECAUSE IT IS WRONG. The harm A-2.3c exists to
    // prevent needs a competing two-term reading: `2d6 d20` could be two terms, so binding it turned
    // one valid parse into a DIFFERENT valid parse. By the time the parser is inside `4d6k`, no
    // such reading is available -- there is nothing for the rule to protect, which is why the fix
    // sits at the top of the modifier loop and not on every read.
    [Theory]
    [InlineData("4d6k h3", 14)]
    [InlineData("4d6kh 3", 14)]
    [InlineData("1d6r 1", 6)]
    public void WhitespaceInsideACommittedModifierStillBinds_UNRULED(string expression, int expected)
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate(expression);

        Assert.True(outcome.Evaluated, $"'{expression}' was refused as {outcome.Fault} — see the note above.");
        Assert.Equal(expected, outcome.Total);
    }

    // THE SHARPEST CASE, and the one that says WHICH of two explanations is right. `1d6x 5` could be
    // the 5 BINDING across the space, or the 5 being silently DROPPED leaving a bare `x`. Those look
    // identical on most faces and differ here: bound, it explodes on a 5 and this die shows 6, so
    // nothing explodes and one die is rolled. Dropped, it would be bare-x -- explode on the maximum
    // -- and this die IS the maximum, so it would roll twice for a total of 10.
    //
    // Measured: one roll, total 6. IT BINDS. A pin whose two explanations were indistinguishable
    // would record the number without recording the fact.
    [Fact]
    public void ANumberAfterAModifierLetterBindsRatherThanBeingDropped_UNRULED()
    {
        var roller = new ScriptedDieRoller(6, 4, 2);

        var outcome = new RollEvaluator(roller).Evaluate("1d6x 5");

        Assert.True(outcome.Evaluated, $"'1d6x 5' was refused as {outcome.Fault}.");
        Assert.Equal(6, outcome.Total);
        Assert.Equal(1, roller.Rolls);
    }

    // THE CONTROL, and the half that keeps pin 3 from reading as "whitespace is ignored everywhere".
    // A bare number after a space, with no modifier letter to attach to, is still REFUSED — so the
    // residual is specific to the inside of a committed suffix rather than general looseness. This
    // one IS ruled (A-2.3c) and would be a real regression if it reddened.
    [Theory]
    [InlineData("1d6 5")]
    [InlineData("1d6 x5")]
    [InlineData("2d6 d20")]
    public void ARULEDCaseThatMustStayRefused(string expression)
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(6, 4, 2)).Evaluate(expression);

        Assert.False(outcome.Evaluated, $"'{expression}' evaluated to {outcome.Total}; A-2.3c refuses it.");
        Assert.Equal(RollFault.Malformed, outcome.Fault);
    }
}
