using System;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Builds and recognises file names of the form campaigns.unreadable-{timestamp}.json.</summary>
public static class PreservedCampaignFile
{
    public const string Prefix = "campaigns.unreadable-";

    public const string Suffix = ".json";


    public static bool IsPreservedName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.Contains('/', StringComparison.Ordinal) ||
            name.Contains('\\', StringComparison.Ordinal) ||
            name.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return name.StartsWith(Prefix, StringComparison.Ordinal)
            && name.EndsWith(Suffix, StringComparison.Ordinal)
            && name.Length > Prefix.Length + Suffix.Length;
    }
}
