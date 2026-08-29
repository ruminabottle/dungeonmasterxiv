using System;

namespace DungeonMasterXIV.Rolls;

/// <summary>
/// Turns roll expression text into a total with the dice behind it, or a refusal naming the fault.
/// <b>The whole of PRD-2 R-2.1 and R-2.1a, and nothing else.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS A LEAF AND MUST STAY ONE.</b> It has no caller, no command, no window and no
/// transport, and that is deliberate rather than unfinished. The Spec Owner's ruling (SQ-84) is that
/// "base chat first" governs what a USER can do, and a pure evaluator nobody can reach cannot make
/// the product roll-first. <b>The moment it acquires a surface a user can reach, the build order
/// applies in full</b> — and <i>"the evaluator is already done"</i> is exactly how the pressure to
/// skip that will be phrased.
/// </para>
/// <para>
/// <b>Text in, a result or a named refusal out.</b> No exceptions escape for bad input: R-2.1a makes
/// a refusal the required OUTCOME for untrusted expressions, so a caller has a value it must look at
/// rather than a throw it might not catch.
/// </para>
/// <para>
/// <b>It never knows what a roll MEANS</b> (D-4). It sums, it counts against numbers the user typed,
/// and it stops there. Success/failure resolution is deferred by the human and would first become
/// expressible here — see <see cref="RollComparison"/>, where that line is written on the type that
/// would carry it.
/// </para>
/// </remarks>
public sealed class RollEvaluator
{
    private readonly IDieRoller _roller;
    private readonly RollLimits _limits;

    /// <summary>Builds an evaluator over a die source and a set of bounds.</summary>
    /// <param name="roller">Where die faces come from. A test supplies known ones.</param>
    /// <param name="limits">The bounds untrusted input is held to. Defaults to <see cref="RollLimits.Default"/>.</param>
    public RollEvaluator(IDieRoller roller, RollLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(roller);

        _roller = roller;
        _limits = limits ?? RollLimits.Default;
    }

    /// <summary>Evaluates <paramref name="expression"/>.</summary>
    /// <param name="expression">The text, which may carry a trailing label.</param>
    /// <returns>A total with its dice, or a refusal naming the fault. Never throws for bad input.</returns>
    public RollOutcome Evaluate(string expression)
    {
        var parse = RollParser.Parse(expression ?? string.Empty, _limits);
        if (parse.Fault is not RollFault.None)
        {
            return RollOutcome.Refused(parse.Fault, parse.Message!);
        }

        var state = new RollEvaluation(_roller, _limits);
        var total = Walk(parse.Node!, state);

        return state.Stopped
            ? RollOutcome.Refused(state.Fault, state.Message!)
            : RollOutcome.Rolled(total!.Value, state.Dice, parse.Label);
    }

    private static int? Walk(RollNode node, RollEvaluation state) => node switch
    {
        NumberNode number => number.Value,
        DiceNode dice => DiceTermEvaluator.Evaluate(dice, state),
        NegateNode negate => Negate(Walk(negate.Operand, state), state),
        BinaryNode binary => Binary(binary, state),
        _ => null,
    };

    // CHECKED, and it takes the state so it can REFUSE rather than wrap (BUG-143). -int.MinValue is
    // the one negation that does not fit, and unchecked it answers int.MinValue again -- a negation
    // that returns its own operand, which is the silent-wrong-answer case rather than a crash.
    private static int? Negate(int? value, RollEvaluation state)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return checked(-value.Value);
        }
        catch (OverflowException)
        {
            return OutOfRange(state);
        }
    }

    private static int? OutOfRange(RollEvaluation state)
    {
        state.Refuse(
            RollFault.ResultOutOfRange,
            "The result is too large to work out; totals must fit in a 32-bit integer.");
        return null;
    }

    private static int? Binary(BinaryNode node, RollEvaluation state)
    {
        var left = Walk(node.Left, state);
        if (left is null)
        {
            return null;
        }

        var right = Walk(node.Right, state);
        if (right is null)
        {
            return null;
        }

        if (node.Operator is RollOperator.Divide && right.Value is 0)
        {
            state.Refuse(RollFault.DivisionByZero, "Division by zero.");
            return null;
        }

        // EVERY ARM IS CHECKED, not just the ones that looked risky. Division is in here because
        // int.MinValue / -1 is the single case the hardware cannot wrap, and it throws WITH OR
        // WITHOUT this block -- so it was the one operation already escaping, out of a method
        // documented never to throw. The catch is what converts all of them into a refusal.
        try
        {
            return checked(node.Operator switch
            {
                RollOperator.Add => left.Value + right.Value,
                RollOperator.Subtract => left.Value - right.Value,
                RollOperator.Multiply => left.Value * right.Value,
                _ => left.Value / right.Value,
            });
        }
        catch (OverflowException)
        {
            return OutOfRange(state);
        }
    }
}
