using System.Diagnostics;
using System.Linq;
using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.3 and A-2.3a: malformed notation is refused with a message naming the fault, and a hostile
/// expression is <b>REFUSED, not survived</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CRITERION FAILS A RUN THAT EVENTUALLY RECOVERS</b>, so asserting only that a refusal comes
/// back is not enough — an evaluator that ground through <c>999999d999999</c> for a minute and then
/// answered would pass that and fail A-2.3a. Every hostile case here is therefore timed, and the
/// bound is small enough that grinding cannot hide inside it.
/// </para>
/// <para>
/// <b>And each refusal is checked by its FAULT, not by "some message came back".</b> R-2.1a requires
/// the refusal to name what was wrong; a test that accepted any non-empty message would pass an
/// evaluator that answered everything with the same shrug.
/// </para>
/// </remarks>
public class AHostileExpressionIsRefusedNotSurvivedTests
{
    // Generous enough not to be flaky on a loaded machine, small enough that no amount of real
    // grinding fits inside it. The point is the ORDER of magnitude, not the number.
    private const int RefusalBudgetMs = 2000;

    private static (RollOutcome Outcome, long ElapsedMs) Timed(string expression)
    {
        var evaluator = new RollEvaluator(new SystemDieRoller());
        var clock = Stopwatch.StartNew();
        var outcome = evaluator.Evaluate(expression);
        clock.Stop();

        return (outcome, clock.ElapsedMilliseconds);
    }

    [Fact]
    public void AnEnormousPoolIsRefusedByNameAndAtOnce()
    {
        var (outcome, elapsed) = Timed("999999d999999");

        Assert.False(outcome.Evaluated);
        Assert.Equal(RollFault.TooManyDice, outcome.Fault);
        Assert.InRange(elapsed, 0, RefusalBudgetMs);
    }

    [Fact]
    public void AnEnormousDieIsRefusedByName()
    {
        var (outcome, elapsed) = Timed("1d999999");

        Assert.Equal(RollFault.DieTooLarge, outcome.Fault);
        Assert.InRange(elapsed, 0, RefusalBudgetMs);
    }

    [Fact]
    public void DeepNestingIsRefusedWhileBeingREADRatherThanAfter()
    {
        // 500 deep: far past the nesting bound of 32, and deliberately SHORT ENOUGH to stay under
        // MaxLength. A thousand deep is 2003 characters and is refused as TooLong first -- a correct
        // refusal, but of the wrong bound, and a test that accepted it would not have exercised
        // nesting at all. Both orders are checked: this one, and AVeryLongExpression... below.
        //
        // If the nesting bound were checked on the finished tree rather than while reading, this
        // would already have exhausted the stack -- which A-2.3a counts as a failure and not a
        // performance issue.
        var expression = new string('(', 500) + "1d6" + new string(')', 500);
        Assert.True(expression.Length < RollLimits.Default.MaxLength, "the case must not trip MaxLength");

        var (outcome, elapsed) = Timed(expression);

        Assert.Equal(RollFault.TooDeeplyNested, outcome.Fault);
        Assert.InRange(elapsed, 0, RefusalBudgetMs);
    }

    [Fact]
    public void AnUnboundedExplosionIsStoppedByTheWorkBudget()
    {
        // Every die shows its maximum, so every die explodes, forever. The shape is legal -- 100d6
        // breaches no size bound -- and only the work budget can stop it. This is the case the
        // shape bounds cannot express.
        var evaluator = new RollEvaluator(new FixedDieRoller(6));
        var clock = Stopwatch.StartNew();
        var outcome = evaluator.Evaluate("100d6x");
        clock.Stop();

        Assert.False(outcome.Evaluated);
        Assert.Equal(RollFault.TooMuchWork, outcome.Fault);
        Assert.InRange(clock.ElapsedMilliseconds, 0, RefusalBudgetMs);
    }

    [Fact]
    public void TheWorkBudgetIsWhatStopsItRatherThanExhaustion()
    {
        // Pins the MECHANISM, not just the outcome: the roller is asked for a bounded number of
        // dice. An evaluator that stopped for any other reason would still show a huge roll count.
        var roller = new FixedDieRoller(6);
        new RollEvaluator(roller).Evaluate("100d6x");

        Assert.InRange(roller.Rolls, 1, RollLimits.Default.MaxWork + 1);
    }

    [Fact]
    public void AVeryLongExpressionIsRefusedBeforeItIsParsed()
    {
        var (outcome, elapsed) = Timed(string.Join("+", Enumerable.Repeat("1d6", 5000)));

        Assert.Equal(RollFault.TooLong, outcome.Fault);
        Assert.InRange(elapsed, 0, RefusalBudgetMs);
    }

    [Fact]
    public void ADigitRunTooLongToHoldIsRefusedRatherThanWrapping()
    {
        // A number that overflows must not become a small plausible one.
        var (outcome, _) = Timed(new string('9', 40) + "d6");

        Assert.False(outcome.Evaluated);
    }

    // A-2.3: malformed notation, each fault named.
    [Theory]
    [InlineData("", RollFault.Empty)]
    [InlineData("   ", RollFault.Empty)]
    [InlineData("1d6+", RollFault.Malformed)]
    [InlineData("+", RollFault.Malformed)]
    [InlineData("(1d6", RollFault.UnbalancedParentheses)]
    [InlineData("1d6)", RollFault.Malformed)]
    [InlineData("1d", RollFault.Malformed)]
    [InlineData("d", RollFault.Malformed)]
    [InlineData("1d0", RollFault.NotANumber)]
    [InlineData("1d6/0", RollFault.DivisionByZero)]
    [InlineData("hello", RollFault.Malformed)]
    [InlineData("1d6 & 2", RollFault.Malformed)]
    public void MalformedNotationIsRefusedNamingTheFault(string expression, RollFault expected)
    {
        var outcome = new RollEvaluator(new SystemDieRoller()).Evaluate(expression);

        Assert.False(outcome.Evaluated);
        Assert.Equal(expected, outcome.Fault);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Message));
    }

    [Fact]
    public void ARefusalRecordsNoRoll()
    {
        var roller = new ScriptedDieRoller();
        var outcome = new RollEvaluator(roller).Evaluate("1d6+");

        // A-2.3: "no roll is recorded". Structural faults are decided before any die is rolled.
        Assert.Empty(outcome.Dice);
        Assert.Equal(0, roller.Rolls);
    }

    [Fact]
    public void ARefusalNeverThrows()
    {
        // R-2.1a makes a refusal the required OUTCOME, so a caller has a value rather than a throw
        // it might not catch.
        var outcome = Record.Exception(() => new RollEvaluator(new SystemDieRoller()).Evaluate("((((("));

        Assert.Null(outcome);
    }
}
