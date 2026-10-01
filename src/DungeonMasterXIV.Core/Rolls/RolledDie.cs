namespace DungeonMasterXIV.Rolls;

/// <summary>One rolled die: its sides, the face it landed on, and whether it counts towards the total.</summary>
public readonly record struct RolledDie(int Sides, int Value, bool Kept = true);
