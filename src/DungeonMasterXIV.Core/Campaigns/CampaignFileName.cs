using System;

namespace DungeonMasterXIV.Campaigns;

public static class CampaignFileName
{
    public const string Prefix = "campaign-";

    public const string Suffix = ".json";

    public const string LegacyFileName = "campaigns.json";

    public static string NameFor(Guid campaignId) => $"{Prefix}{campaignId:D}{Suffix}";

    public static bool IsCampaignFileName(string? name) => TryCampaignIdOf(name, out _);

    public static bool TryCampaignIdOf(string? name, out Guid campaignId)
    {
        campaignId = Guid.Empty;

        if (string.IsNullOrEmpty(name) ||
            !name.StartsWith(Prefix, StringComparison.Ordinal) ||
            !name.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var middle = name[Prefix.Length..^Suffix.Length];
        return Guid.TryParseExact(middle, "D", out campaignId);
    }
}
