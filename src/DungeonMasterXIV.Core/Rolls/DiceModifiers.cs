namespace DungeonMasterXIV.Rolls;

/// <summary>The modifiers on a dice term: keep or drop highest or lowest, reroll, explode, and count successes.</summary>
public sealed record DiceModifiers
{
    public static DiceModifiers None { get; } = new();

    public int? KeepHighest { get; init; }

    public int? KeepLowest { get; init; }

    public int? DropHighest { get; init; }

    public int? DropLowest { get; init; }

    public RollComparison? Reroll { get; init; }

    public RollComparison? Explode { get; init; }

    public bool ExplodeOnMaximum { get; init; }

    public RollComparison? CountSuccesses { get; init; }

    public bool Any =>
        KeepHighest is not null || KeepLowest is not null || DropHighest is not null
        || DropLowest is not null || Reroll is not null || Explode is not null
        || ExplodeOnMaximum || CountSuccesses is not null;
}
