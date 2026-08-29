using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Rolls;

/// <summary>
/// Rolls one dice term and applies its modifiers, in the order the grammar means them: roll, reroll,
/// explode, keep/drop, then count or sum.
/// </summary>
/// <remarks>
/// <b>The order is fixed and is not the order they were written.</b> A reroll replaces a die before
/// anything looks at it; an explosion adds dice that are themselves subject to keep/drop; a keep
/// decides which dice count; and only then is the term summed or counted. Writing
/// <c>4d6&gt;3kh3</c> and <c>4d6kh3&gt;3</c> means the same roll, because the suffixes are a set and
/// not a pipeline — which is why <see cref="DiceModifiers"/> is one record rather than nested
/// wrappers carrying an order the grammar cannot express.
/// </remarks>
internal static class DiceTermEvaluator
{
    /// <summary>Rolls <paramref name="dice"/> and returns its value, or null if a bound stopped it.</summary>
    public static int? Evaluate(DiceNode dice, RollEvaluation state)
    {
        var first = state.RecordedCount;

        for (var i = 0; i < dice.Count; i++)
        {
            if (RollWithRerolls(dice, state) is null)
            {
                return null;
            }
        }

        if (!Explode(dice, state, first))
        {
            return null;
        }

        ApplyKeepAndDrop(dice.Modifiers, state, first);
        return Combine(dice.Modifiers, state, first);
    }

    private static int? RollWithRerolls(DiceNode dice, RollEvaluation state)
    {
        var value = state.RollOne(dice.Sides);
        if (value is null)
        {
            return null;
        }

        // One reroll per die: the discarded face stays in the result as not-kept, so a reader sees
        // the reroll happened rather than inferring it from a total.
        if (dice.Modifiers.Reroll is { } reroll && reroll.Matches(value.Value))
        {
            state.Record(dice.Sides, value.Value, kept: false);
            value = state.RollOne(dice.Sides);
            if (value is null)
            {
                return null;
            }
        }

        state.Record(dice.Sides, value.Value, kept: true);
        return value;
    }

    private static bool Explode(DiceNode dice, RollEvaluation state, int first)
    {
        if (dice.Modifiers.Explode is null && !dice.Modifiers.ExplodeOnMaximum)
        {
            return true;
        }

        // Walks forward over dice this term added, including ones added by this loop, so a chain of
        // explosions is followed to its end -- bounded only by the work budget, which is the point.
        for (var i = first; i < state.RecordedCount; i++)
        {
            var die = state.Dice[i];
            if (!die.Kept || !Explodes(dice.Modifiers, die))
            {
                continue;
            }

            var value = state.RollOne(dice.Sides);
            if (value is null)
            {
                return false;
            }

            state.Record(dice.Sides, value.Value, kept: true);
        }

        return true;
    }

    // BUG-144: the bare-x case is now asked as a QUESTION ABOUT THE MODIFIER rather than recognised
    // by comparing against a value, so an identical-looking value the user typed cannot answer yes.
    private static bool Explodes(DiceModifiers modifiers, RolledDie die) =>
        modifiers.ExplodeOnMaximum
            ? die.Value == die.Sides
            : modifiers.Explode is { } explode && explode.Matches(die.Value);

    private static void ApplyKeepAndDrop(DiceModifiers modifiers, RollEvaluation state, int first)
    {
        var kept = Enumerable.Range(first, state.RecordedCount - first)
            .Where(i => state.Dice[i].Kept)
            .ToList();

        var keeping = Keeping(modifiers, kept.Count);
        if (keeping is null)
        {
            return;
        }

        var ordered = modifiers.KeepLowest is not null || modifiers.DropHighest is not null
            ? kept.OrderBy(i => state.Dice[i].Value).ToList()
            : kept.OrderByDescending(i => state.Dice[i].Value).ToList();

        foreach (var index in ordered.Skip(keeping.Value))
        {
            state.SetKept(index, kept: false);
        }
    }

    private static int? Keeping(DiceModifiers modifiers, int rolled)
    {
        if (modifiers.KeepHighest is { } kh)
        {
            return System.Math.Min(kh, rolled);
        }

        if (modifiers.KeepLowest is { } kl)
        {
            return System.Math.Min(kl, rolled);
        }

        if (modifiers.DropLowest is { } dl)
        {
            return System.Math.Max(rolled - dl, 0);
        }

        return modifiers.DropHighest is { } dh ? System.Math.Max(rolled - dh, 0) : null;
    }

    private static int Combine(DiceModifiers modifiers, RollEvaluation state, int first)
    {
        var kept = Kept(state, first);

        // Counting successes against a number the USER TYPED is arithmetic and in scope (R-2.1).
        // It yields a COUNT. Nothing here decides whether a roll succeeded -- see RollComparison.
        return modifiers.CountSuccesses is { } test
            ? kept.Count(test.Matches)
            : kept.Sum();
    }

    private static IEnumerable<int> Kept(RollEvaluation state, int first) =>
        Enumerable.Range(first, state.RecordedCount - first)
            .Where(i => state.Dice[i].Kept)
            .Select(i => state.Dice[i].Value);
}
