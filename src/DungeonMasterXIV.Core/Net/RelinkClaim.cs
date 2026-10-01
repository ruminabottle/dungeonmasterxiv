namespace DungeonMasterXIV.Net;

public readonly record struct RelinkClaim(bool Matched, string? Label)
{
    public static RelinkClaim None => default;
}
