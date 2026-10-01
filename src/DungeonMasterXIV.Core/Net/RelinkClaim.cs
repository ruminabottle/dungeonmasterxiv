namespace DungeonMasterXIV.Net;

/// <summary>Whether a join request matched a known campaign participant, and that participant's label.</summary>
public readonly record struct RelinkClaim(bool Matched, string? Label)
{
    public static RelinkClaim None => default;
}
