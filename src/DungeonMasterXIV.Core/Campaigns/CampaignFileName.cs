using System;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Naming for the one-file-per-campaign layout A-1.11b requires.
/// </summary>
/// <remarks>
/// Here rather than in the file adapter for the same reason as
/// <see cref="PreservedCampaignFile"/>: these names reach file writes and deletes, so the guard
/// that decides whether a name is one of ours has to be testable.
/// </remarks>
public static class CampaignFileName
{
    /// <summary>What every campaign file's name starts with.</summary>
    public const string Prefix = "campaign-";

    /// <summary>What every campaign file's name ends with.</summary>
    public const string Suffix = ".json";

    /// <summary>
    /// The single-file layout this plugin used before A-1.11b. Read once, migrated, and deleted;
    /// never written again.
    /// </summary>
    public const string LegacyFileName = "campaigns.json";

    /// <summary>The file a campaign is stored in.</summary>
    /// <param name="campaignId">The campaign's local UUID.</param>
    public static string NameFor(Guid campaignId) => $"{Prefix}{campaignId:D}{Suffix}";

    /// <summary>
    /// Whether this is a campaign file name this plugin wrote, and so one it may read or delete.
    /// Rejects anything carrying a path, and anything whose middle is not a UUID.
    /// </summary>
    /// <param name="name">A bare file name.</param>
    public static bool IsCampaignFileName(string? name) => TryCampaignIdOf(name, out _);

    /// <summary>Recovers the campaign UUID a file name encodes.</summary>
    /// <param name="name">A bare file name.</param>
    /// <param name="campaignId">The UUID, when the name is one of ours.</param>
    public static bool TryCampaignIdOf(string? name, out Guid campaignId)
    {
        campaignId = Guid.Empty;

        // No separate check for separators or "..". A strict Guid "D" parse admits only hex and
        // hyphens, so a middle containing '/', '\\' or '.' cannot parse and is already rejected
        // below. An explicit check for them here could never fire -- dead because of a property of
        // the format rather than because of who calls it, which is the case where removing it is
        // right rather than merely tidy. PreservedCampaignFile is NOT the same: its middle is a
        // free-form timestamp, so its separator checks are load-bearing and stay.
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
