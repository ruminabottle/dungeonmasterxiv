using System;

namespace DungeonMasterXIV.Rolls;

/// <summary>
/// Reads expression text into a <see cref="RollNode"/>, or refuses it naming the fault.
/// </summary>
/// <remarks>
/// <para>
/// Recursive descent, standard precedence: <c>+ -</c> below <c>* /</c> below unary minus below
/// primaries. Parentheses nest, and <c>d20</c> means <c>1d20</c> (R-2.1).
/// </para>
/// <para>
/// <b>THE NESTING BOUND IS ENFORCED WHILE READING, NOT AFTER.</b> A depth counter is carried through
/// the descent and checked on entering a parenthesis, so <c>((((…))))</c> a thousand deep is refused
/// at depth 33 rather than after the stack has already gone. A bound that is checked once the tree
/// exists has already paid the cost it exists to prevent — and R-2.1a puts a crash in the same
/// category as a wrong answer.
/// </para>
/// </remarks>
internal sealed class RollParser
{
    private readonly RollCursor _cursor;
    private readonly RollLimits _limits;

    private RollParser(string text, RollLimits limits)
    {
        _cursor = new RollCursor(text);
        _limits = limits;
    }

    /// <summary>Reads <paramref name="text"/>, applying the shape bounds in <paramref name="limits"/>.</summary>
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

    /// <summary>
    /// Separates a trailing free-text label from the expression — <c>1d20+5 [perception]</c> or
    /// <c>1d20+5 #perception</c>.
    /// </summary>
    /// <remarks>
    /// <b>Split lexically and never read.</b> D-4: the plugin stores and displays a label and never
    /// interprets it. Taking it off before parsing is what keeps that true by construction — no
    /// grammar rule can branch on text the grammar never sees.
    /// </remarks>
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

        // A leading 'd' with no count is 1dN -- R-2.1's "d20 means 1d20".
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
