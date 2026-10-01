namespace DungeonMasterXIV.Rolls;

internal static class RollDiceParser
{
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
            if (cursor.AtWhitespace)
            {
                return RollParse.Parsed(dice with { Modifiers = modifiers }, null);
            }

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

        if (cursor.TakeLetter('d'))
        {
            return Drop(cursor, current);
        }

        if (cursor.TakeLetter('r'))
        {
            return Comparison(cursor, out var reroll)
                ? new ModifierParse(current with { Reroll = reroll }, RollFault.None, null)
                : Bad(cursor, "a reroll test");
        }

        if (cursor.TakeLetter('x'))
        {
            return Comparison(cursor, out var explode)
                ? new ModifierParse(
                    current with { Explode = explode, ExplodeOnMaximum = false }, RollFault.None, null)
                : new ModifierParse(
                    current with { Explode = null, ExplodeOnMaximum = true }, RollFault.None, null);
        }

        if (cursor.Peek() is '>' or '<' or '=')
        {
            return Comparison(cursor, out var success)
                ? new ModifierParse(current with { CountSuccesses = success }, RollFault.None, null)
                : Bad(cursor, "a success test");
        }

        return new ModifierParse(null, RollFault.None, null);
    }

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
            high ? OnlyKeepDrop(current) with { KeepHighest = howMany }
                 : OnlyKeepDrop(current) with { KeepLowest = howMany },
            RollFault.None,
            null);
    }

    private static ModifierParse Drop(RollCursor cursor, DiceModifiers current)
    {
        var low = !cursor.TakeLetter('h');
        if (low)
        {
            cursor.TakeLetter('l');
        }

        if (!cursor.TryNumber(out var howMany))
        {
            return Bad(cursor, "a number of dice to drop");
        }

        return new ModifierParse(
            low ? OnlyKeepDrop(current) with { DropLowest = howMany }
                : OnlyKeepDrop(current) with { DropHighest = howMany },
            RollFault.None,
            null);
    }

    private static DiceModifiers OnlyKeepDrop(DiceModifiers current) =>
        current with { KeepHighest = null, KeepLowest = null, DropHighest = null, DropLowest = null };

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
