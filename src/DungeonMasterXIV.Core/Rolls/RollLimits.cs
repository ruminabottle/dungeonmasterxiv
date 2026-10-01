namespace DungeonMasterXIV.Rolls;

public sealed record RollLimits
{
    public static RollLimits Default { get; } = new();

    public int MaxDicePerTerm { get; init; } = 1000;

    public int MaxDieSize { get; init; } = 10000;

    public int MaxNestingDepth { get; init; } = 32;

    public int MaxWork { get; init; } = 20000;

    public int MaxLength { get; init; } = 2000;
}
