using System;

namespace DungeonMasterXIV.Rolls;

internal sealed class RollParser
{
    private readonly RollCursor _cursor;
    private readonly RollLimits _limits;

    private RollParser(string text, RollLimits limits)
    {
        _cursor = new RollCursor(text);
        _limits = limits;
    }

    public static RollParse Parse(string text, RollLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (string.IsNullOrWhiteSpace(text))
        {
            return RollParse.Refused(RollFault.Empty, "The expression was empty.");
        }

        if (text.Length > limits.MaxLength)
        {
            return RollParse.Refused(
                RollFault.TooLong,
                $"The expression was {text.Length} characters; the limit is {limits.MaxLength}.");
        }

        var (body, label) = SplitLabel(text);
        return new RollParser(body, limits).ParseAll(label);
    }

    private static (string Body, string? Label) SplitLabel(string text)
    {
        var hash = text.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            return (text[..hash], Trimmed(text[(hash + 1)..]));
        }

        var open = text.IndexOf('[', StringComparison.Ordinal);
        if (open >= 0 && text.EndsWith(']'))
        {
            return (text[..open], Trimmed(text[(open + 1)..^1]));
        }

        return (text, null);
    }

    private static string? Trimmed(string value) =>
        value.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    private RollParse ParseAll(string? label)
    {
        var parse = ParseExpression(0);
        if (parse.Fault is not RollFault.None)
        {
            return parse;
        }

        if (!_cursor.AtEnd)
        {
            return RollParse.Refused(
                RollFault.Malformed,
                $"Unexpected '{_cursor.Peek()}' at position {_cursor.Position}.");
        }

        return RollParse.Parsed(parse.Node!, label);
    }

    private RollParse ParseExpression(int depth)
    {
        var left = ParseTerm(depth);
        if (left.Fault is not RollFault.None)
        {
            return left;
        }

        var node = left.Node!;
        while (_cursor.Peek() is '+' or '-')
        {
            var op = _cursor.Take('+') ? RollOperator.Add : Consume('-', RollOperator.Subtract);
            var right = ParseTerm(depth);
            if (right.Fault is not RollFault.None)
            {
                return right;
            }

            node = new BinaryNode(op, node, right.Node!);
        }

        return RollParse.Parsed(node, null);
    }

    private RollParse ParseTerm(int depth)
    {
        var left = ParseUnary(depth);
        if (left.Fault is not RollFault.None)
        {
            return left;
        }

        var node = left.Node!;
        while (_cursor.Peek() is '*' or '/')
        {
            var op = _cursor.Take('*') ? RollOperator.Multiply : Consume('/', RollOperator.Divide);
            var right = ParseUnary(depth);
            if (right.Fault is not RollFault.None)
            {
                return right;
            }

            node = new BinaryNode(op, node, right.Node!);
        }

        return RollParse.Parsed(node, null);
    }

    private RollParse ParseUnary(int depth)
    {
        if (!_cursor.Take('-'))
        {
            return ParsePrimary(depth);
        }

        var operand = ParseUnary(depth);
        return operand.Fault is not RollFault.None
            ? operand
            : RollParse.Parsed(new NegateNode(operand.Node!), null);
    }

    private RollParse ParsePrimary(int depth)
    {
        if (_cursor.Take('('))
        {
            return ParseParenthesised(depth);
        }

        if (_cursor.Peek() is 'd' or 'D')
        {
            _cursor.TakeLetter('d');
            return RollDiceParser.ParseDice(_cursor, _limits, 1);
        }

        if (!_cursor.TryNumber(out var value))
        {
            return RollParse.Refused(
                RollFault.Malformed,
                $"Expected a number or dice at position {_cursor.Position}.");
        }

        if (!_cursor.TakeLetter('d'))
        {
            return RollParse.Parsed(new NumberNode(value), null);
        }

        return RollDiceParser.ParseDice(_cursor, _limits, value);
    }

    private RollParse ParseParenthesised(int depth)
    {
        if (depth + 1 > _limits.MaxNestingDepth)
        {
            return RollParse.Refused(
                RollFault.TooDeeplyNested,
                $"Nesting went deeper than {_limits.MaxNestingDepth}.");
        }

        var inner = ParseExpression(depth + 1);
        if (inner.Fault is not RollFault.None)
        {
            return inner;
        }

        return _cursor.Take(')')
            ? inner
            : RollParse.Refused(RollFault.UnbalancedParentheses, "A '(' was never closed.");
    }

    private RollOperator Consume(char c, RollOperator op)
    {
        _cursor.Take(c);
        return op;
    }
}
