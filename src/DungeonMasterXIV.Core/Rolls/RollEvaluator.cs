using System;

namespace DungeonMasterXIV.Rolls;

public sealed class RollEvaluator
{
    private readonly IDieRoller _roller;
    private readonly RollLimits _limits;

    public RollEvaluator(IDieRoller roller, RollLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(roller);

        _roller = roller;
        _limits = limits ?? RollLimits.Default;
    }

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
