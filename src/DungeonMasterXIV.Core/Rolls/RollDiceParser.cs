namespace DungeonMasterXIV.Rolls;

/// <summary>
/// Reads a dice term and its modifiers: <c>4d6kh3</c>, <c>10d10x&gt;9</c>, <c>6d6r1</c>,
/// <c>5d10&gt;7</c>.
/// </summary>
/// <remarks>
/// <b>Separate from <see cref="RollParser"/> because the two grammars are different shapes.</b>
/// Arithmetic is precedence and recursion; a dice term is a flat run of suffixes with no precedence
/// among them. Keeping them in one type would have produced a single class carrying both, which is
/// the size problem this chunk was warned about before a line was written.
/// </remarks>
internal static class RollDiceParser
{
    /// <summary>Reads the die size and any modifiers, given the already-read <paramref name="count"/>.</summary>
    public static RollParse ParseDice(RollCursor cursor, RollLimits limits, int count)
    {
        if (!cursor.TryNumber(out var sides))
        {
            return RollParse.Refused(
                RollFault.Malformed,
                $"Expected a die size after 'd' at position {cursor.Position}.");
        }

        if (count > limits.MaxDicePerTerm)
        {
            return RollParse.Refused(
                RollFault.TooManyDice,
                $"{count} dice in one term; the limit is {limits.MaxDicePerTerm}.");
        }

        if (sides < 1)
        {
            return RollParse.Refused(RollFault.NotANumber, "A die must have at least one face.");
        }

        if (sides > limits.MaxDieSize)
        {
            return RollParse.Refused(
                RollFault.DieTooLarge,
                $"A d{sides} exceeds the largest die of d{limits.MaxDieSize}.");
        }

        return ParseModifiers(cursor, new DiceNode(count, sides, DiceModifiers.None));
    }

    private static RollParse ParseModifiers(RollCursor cursor, DiceNode dice)
    {
        var modifiers = dice.Modifiers;

        while (true)
        {
            var next = ParseOne(cursor, modifiers);
            if (next.Fault is not RollFault.None)
            {
                return RollParse.Refused(next.Fault, next.Message!);
            }

            if (next.Modifiers is null)
            {
                return RollParse.Parsed(dice with { Modifiers = modifiers }, null);
            }

            modifiers = next.Modifiers;
        }
    }

    private static ModifierParse ParseOne(RollCursor cursor, DiceModifiers current)
    {
        if (cursor.TakeLetter('k'))
        {
            return Keep(cursor, current);
        }

        if (cursor.TakeLetter('r'))
        {
            return Comparison(cursor, out var reroll)
                ? new ModifierParse(current with { Reroll = reroll }, RollFault.None, null)
                : Bad(cursor, "a reroll test");
        }

        if (cursor.TakeLetter('x'))
        {
            // A bare 'x' explodes on the maximum face; the size is not known here, so the evaluator
            // resolves it. Represented as a null-value comparison would be ambiguous, so the
            // evaluator reads Explode with Value 0 and Equal as "on the maximum".
            return Comparison(cursor, out var explode)
                ? new ModifierParse(current with { Explode = explode }, RollFault.None, null)
                : new ModifierParse(current with { Explode = ExplodeOnMaximum }, RollFault.None, null);
        }

        if (cursor.Peek() is '>' or '<' or '=')
        {
            return Comparison(cursor, out var success)
                ? new ModifierParse(current with { CountSuccesses = success }, RollFault.None, null)
                : Bad(cursor, "a success test");
        }

        return new ModifierParse(null, RollFault.None, null);
    }

    /// <summary>The sentinel for a bare <c>x</c>: explode when the die shows its own maximum.</summary>
    public static RollComparison ExplodeOnMaximum { get; } = new(ComparisonOperator.Equal, 0);

    private static ModifierParse Keep(RollCursor cursor, DiceModifiers current)
    {
        var high = !cursor.TakeLetter('l');
        if (high)
        {
            cursor.TakeLetter('h');
        }

        if (!cursor.TryNumber(out var howMany))
        {
            return Bad(cursor, "a number of dice to keep");
        }

        return new ModifierParse(
            high ? current with { KeepHighest = howMany } : current with { KeepLowest = howMany },
            RollFault.None,
            null);
    }

    /// <summary>
    /// Reads a test: an operator and a number, or <b>a bare number meaning equality</b>.
    /// </summary>
    /// <remarks>
    /// <c>r1</c> is "reroll a 1" and <c>x6</c> is "explode on a 6" — the grammar lets the operator be
    /// omitted when it is <c>=</c>, which is the common case for both. Success counting does not
    /// reach here without an operator, because a bare number after a dice term is not a test at all.
    /// </remarks>
    private static bool Comparison(RollCursor cursor, out RollComparison comparison)
    {
        comparison = default;
        var op = ReadOperator(cursor);

        if (!cursor.TryNumber(out var value))
        {
            return false;
        }

        comparison = new RollComparison(op ?? ComparisonOperator.Equal, value);
        return true;
    }

    private static ComparisonOperator? ReadOperator(RollCursor cursor)
    {
        if (cursor.Take('>'))
        {
            return cursor.Take('=') ? ComparisonOperator.AtLeast : ComparisonOperator.Greater;
        }

        if (cursor.Take('<'))
        {
            return cursor.Take('=') ? ComparisonOperator.AtMost : ComparisonOperator.Less;
        }

        return cursor.Take('=') ? ComparisonOperator.Equal : null;
    }

    private static ModifierParse Bad(RollCursor cursor, string expected) =>
        new(null, RollFault.Malformed, $"Expected {expected} at position {cursor.Position}.");

    private readonly record struct ModifierParse(DiceModifiers? Modifiers, RollFault Fault, string? Message);
}
