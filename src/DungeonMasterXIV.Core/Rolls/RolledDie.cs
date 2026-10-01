namespace DungeonMasterXIV.Rolls;

public readonly record struct RolledDie(int Sides, int Value, bool Kept = true);
