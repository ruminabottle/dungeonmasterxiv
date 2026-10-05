using System;
using System.Text;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Reads and records a campaign's display-name alias, else a carried-over default or the character name.</summary>
/// <remarks>A null alias means no name was ever chosen for the campaign; an empty one means the player chose
/// their character name, so the carried-over default is not offered again (rolls R-2.17).</remarks>
public static class CampaignDisplayName
{
    public static string Stored(Campaign? campaign) => campaign?.DisplayNameAlias ?? string.Empty;

    public static bool Record(Campaign? campaign, string? alias)
    {
        if (campaign is null)
        {
            return false;
        }

        var trimmed = string.IsNullOrWhiteSpace(alias) ? string.Empty : alias.Trim();

        if (string.Equals(campaign.DisplayNameAlias, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        campaign.DisplayNameAlias = trimmed;
        return true;
    }

    public static Net.DisplayName Or(Campaign? campaign, Net.DisplayName characterName) =>
        Net.DisplayName.TryParse(Stored(campaign), out var alias) ? alias : characterName;

    public static string ToEdit(Campaign? campaign, Net.DisplayName characterName) =>
        ToEdit(campaign, carriedOverDefault: null, characterName);

    public static string ToEdit(
        Campaign? campaign, string? carriedOverDefault, Net.DisplayName characterName) =>
        campaign?.DisplayNameAlias switch
        {
            { Length: > 0 } stored => stored,
            not null => characterName.Value,
            null when carriedOverDefault is { Length: > 0 } carried => carried,
            null => characterName.Value,
        };

    public static string ToPreFill(
        Campaign? campaign, string? carriedOverDefault, Net.DisplayName characterName) =>
        ToEdit(campaign, campaign is null ? null : carriedOverDefault, characterName);

    public static bool RecordChosen(Campaign? campaign, string? typed, Net.DisplayName characterName)
    {
        var trimmed = string.IsNullOrWhiteSpace(typed) ? string.Empty : typed.Trim();

        if (WouldShortenANameTheFieldCouldNotShow(Stored(campaign), trimmed))
        {
            return false;
        }

        return Record(
            campaign,
            string.Equals(trimmed, characterName.Value, StringComparison.Ordinal)
                ? string.Empty
                : trimmed);
    }

    private static bool WouldShortenANameTheFieldCouldNotShow(string stored, string incoming) =>
        incoming.Length > 0
        && Net.NameInputCapacity.IsFull(stored)
        && Encoding.UTF8.GetByteCount(incoming) < Encoding.UTF8.GetByteCount(stored);
}
