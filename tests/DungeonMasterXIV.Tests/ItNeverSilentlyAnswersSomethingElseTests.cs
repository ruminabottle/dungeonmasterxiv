using System.Linq;
using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.1: <i>"Malformed notation is refused with a message naming what was wrong. It never silently
/// rolls something else."</i> BUG-143 and BUG-144 are two breaches of that one sentence.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are together because the requirement is one sentence, not because it was convenient.</b>
/// Unchecked arithmetic answered <c>2000000000+2000000000</c> as <c>-294967296</c>, and a writable
/// sentinel answered <c>1d6x0</c> as though <c>1d6x</c> had been written. Different mechanisms,
/// identical harm: a confident number for an expression the user did not write. A refusal is
/// recoverable and a wrong total is not, because nothing downstream can tell it from a right one.
/// </para>
/// <para>
/// <b>The throwing case is the same defect, not a worse one.</b> <c>int.MinValue / -1</c> is the one
/// result the hardware cannot wrap, so it raised <c>OverflowException</c> out of a method whose own
/// summary promises it <i>"never throws for bad input"</i>. Wrapping and throwing are the two ways
/// the same missing check surfaces — which is why one <c>checked</c> block fixes both.
/// </para>
/// </remarks>
public class ItNeverSilentlyAnswersSomethingElseTests
{
    private static RollOutcome Evaluate(string expression) =>
        new RollEvaluator(new ScriptedDieRoller()).Evaluate(expression);

    // BUG-143, the wrapping half. Each of these previously came back Evaluated with a confident
    // wrong number, which is the outcome R-2.1 names and forbids.
    [Theory]
    [InlineData("1073741824*2")]
    [InlineData("2000000000+2000000000")]
    [InlineData("2000000000*2000000000")]
    public void AResultThatDoesNotFitIsRefusedRatherThanWrapped(string expression)
    {
        var outcome = Evaluate(expression);

        Assert.False(outcome.Evaluated, $"'{expression}' answered {outcome.Total} instead of refusing.");
        Assert.Equal(RollFault.ResultOutOfRange, outcome.Fault);
        Assert.NotNull(outcome.Message);
    }

    // BUG-143, the throwing half, and the assertion is that CONTROL RETURNS AT ALL. Before the fix
    // this did not fail an assertion -- it escaped the method, so a test asserting on the outcome
    // never got one to assert about.
    [Fact]
    public void TheOneResultTheHardwareCannotWrapIsRefusedRatherThanThrown()
    {
        var outcome = Record.Exception(() => Evaluate("1073741824*2/-1")) is { } thrown
            ? throw new Xunit.Sdk.XunitException(
                $"Evaluate threw {thrown.GetType().Name}, and its summary promises it never throws "
                + $"for bad input: {thrown.Message}")
            : Evaluate("1073741824*2/-1");

        Assert.False(outcome.Evaluated);
        Assert.Equal(RollFault.ResultOutOfRange, outcome.Fault);
    }

    // CONTROL for both of the above, and the half that stops the fix from being "refuse everything
    // large". A result near the edge that DOES fit must still be answered.
    [Theory]
    [InlineData("0-2147483639-1", -2147483640)]
    // 2147483639, not int.MaxValue: RollCursor.TryNumber guards on the 9-digit prefix
    // (value > (int.MaxValue - 9) / 10), so 2147483640..2147483647 are refused as Malformed.
    // Pre-existing, conservative in the SAFE direction -- it refuses rather than wraps -- and
    // outside this assignment. Reported rather than changed; pinned here so the boundary is
    // recorded rather than rediscovered.
    [InlineData("2147483639", 2147483639)]
    [InlineData("1073741823*2", 2147483646)]
    [InlineData("2+2", 4)]
    public void ArithmeticThatFitsIsStillAnswered(string expression, int expected)
    {
        var outcome = Evaluate(expression);

        Assert.True(outcome.Evaluated, $"'{expression}' was refused as {outcome.Fault}.");
        Assert.Equal(expected, outcome.Total);
    }

    // BUG-144. x0 asks to explode on a face a die never shows, so nothing explodes. Before the fix
    // it produced the identical RollComparison the bare-x sentinel used, and exploded on the maximum.
    [Theory]
    [InlineData("1d6x0")]
    [InlineData("1d6x=0")]
    public void ExplodingOnAFaceNoDieShowsRollsOnceAndStops(string expression)
    {
        var roller = new ScriptedDieRoller(6, 4, 2);

        var outcome = new RollEvaluator(roller).Evaluate(expression);

        Assert.True(outcome.Evaluated, $"'{expression}' was refused as {outcome.Fault}.");
        Assert.Equal(6, outcome.Total);
        Assert.Equal(1, roller.Rolls);
    }

    // THE CONTROL THAT MAKES THE ROW ABOVE MEAN SOMETHING. If exploding had simply been broken, the
    // x0 rows would pass for entirely the wrong reason -- so the two forms that SHOULD explode are
    // asserted against the same roller and the same faces.
    [Theory]
    [InlineData("1d6x")]
    [InlineData("1d6x6")]
    [InlineData("1d6x>5")]
    public void ExplodingOnAFaceTheDieDoesShowStillChains(string expression)
    {
        var roller = new ScriptedDieRoller(6, 4, 2);

        var outcome = new RollEvaluator(roller).Evaluate(expression);

        Assert.True(outcome.Evaluated, $"'{expression}' was refused as {outcome.Fault}.");
        Assert.Equal(10, outcome.Total);
        Assert.Equal(2, roller.Rolls);
    }

    // BUG-149. THE PREMISE FIRST, because it is the half I got wrong last time: int.MinValue is
    // REACHABLE. I recorded the Negate guard as defensive against a path that does not exist, on the
    // grounds that arithmetic producing int.MinValue is refused. That is true of MULTIPLICATION and
    // false in general -- int.MinValue is a representable result, so a SUBTRACTION reaches it with
    // nothing for the checked block to refuse. I reasoned from the operation I had mutated rather
    // than from the value.
    [Theory]
    [InlineData("0-1073741824-1073741824")]
    [InlineData("0-2147483639-9")]
    public void TheMostNegativeValueIsReachableAsAResult(string expression)
    {
        var outcome = Evaluate(expression);

        Assert.True(outcome.Evaluated, $"'{expression}' was refused as {outcome.Fault}.");
        Assert.Equal(int.MinValue, outcome.Total);
    }

    // ...AND THEREFORE THE NEGATE GUARD IS LIVE. Deleting its catch rethrows out of Evaluate, which
    // is BUG-143's throwing half returning by the one door that had no test on it.
    [Fact]
    public void NegatingTheMostNegativeValueIsRefusedRatherThanThrown()
    {
        const string Expression = "-(0-1073741824-1073741824)";

        var thrown = Record.Exception(() => Evaluate(Expression));

        Assert.True(
            thrown is null,
            $"Evaluate threw {thrown?.GetType().Name}, and its summary promises it never throws for "
            + $"bad input: {thrown?.Message}");

        var outcome = Evaluate(Expression);

        Assert.False(outcome.Evaluated);
        Assert.Equal(RollFault.ResultOutOfRange, outcome.Fault);
    }

    // BUG-148. LAST SUFFIX WINS, which is this grammar's existing convention for exploding rather
    // than a new rule invented here.
    //
    // THE ROWS ARE CHOSEN TO DISTINGUISH, which took some care: 4d6kh3dh1 answers 9 under BOTH the
    // shipped defect and the fix, because "keep the three lowest" and "drop the highest" select the
    // same dice from these faces. A row like that is coverage and not evidence. Each row below
    // differs between the two readings.
    [Theory]
    // kh3 then dh2: defect kept 3 lowest (9); last-wins drops the 2 highest, keeping 1 and 3.
    [InlineData("4d6kh3dh2", 4)]
    // the k arm ALONE, which parsed long before the drop half existed -- this is why the bug is
    // pre-existing rather than something the BUG-142 fix introduced.
    [InlineData("4d6kh1kl2", 4)]
    // and the reverse order, to show the rule is positional rather than a precedence among suffixes.
    [InlineData("4d6kl2kh1", 6)]
    public void TheLastKeepOrDropSuffixWinsRatherThanCombiningWithTheOthers(string expression, int expected)
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate(expression);

        Assert.True(outcome.Evaluated, $"'{expression}' was refused as {outcome.Fault}.");
        Assert.Equal(expected, outcome.Total);
    }

    // THE DEFECT NAMED DIRECTLY: the count came from one suffix and the direction from another, so
    // adding dh1 to kh3 inverted which end was kept. This asserts the END, which is the thing a
    // total cannot always show.
    [Fact]
    public void AddingADropSuffixDoesNotInvertWhichEndAKeepSuffixKept()
    {
        var outcome = new RollEvaluator(new ScriptedDieRoller(1, 6, 3, 5)).Evaluate("4d6kh3dh2");

        var kept = outcome.Dice.Where(die => die.Kept).Select(die => die.Value).OrderBy(v => v);

        Assert.Equal(new[] { 1, 3 }, kept);
    }

    // AND THE SENTINEL IS NO LONGER WRITABLE AT ALL, which is the property rather than the instance.
    // x0 was the only value that collided, so a fix targeting "0" would pass every row above while
    // leaving the shape intact. This asserts the two are carried SEPARATELY: an explicit test and
    // the bare form disagree about a die showing its maximum only if they are distinct fields.
    [Fact]
    public void TheBareFormAndAnExplicitZeroAreNotTheSameModifier()
    {
        var bare = new RollEvaluator(new ScriptedDieRoller(6, 4, 2)).Evaluate("1d6x");
        var zero = new RollEvaluator(new ScriptedDieRoller(6, 4, 2)).Evaluate("1d6x0");

        Assert.NotEqual(bare.Total, zero.Total);
    }
}
