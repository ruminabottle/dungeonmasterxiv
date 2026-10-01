using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Rolls;

internal static class DiceTermEvaluator
{
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

        return modifiers.CountSuccesses is { } test
            ? kept.Count(test.Matches)
            : kept.Sum();
    }

    private static IEnumerable<int> Kept(RollEvaluation state, int first) =>
        Enumerable.Range(first, state.RecordedCount - first)
            .Where(i => state.Dice[i].Kept)
            .Select(i => state.Dice[i].Value);
}
