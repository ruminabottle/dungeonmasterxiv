using System;
using System.Globalization;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Names a campaign: its stored name, or its local creation date and time without a weekday.</summary>
public static class CampaignName
{
    public static string For(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return string.IsNullOrWhiteSpace(campaign.Name)
            ? Auto(campaign.CreatedUtc)
            : campaign.Name!;
    }

    public static string Auto(DateTimeOffset createdUtc, CultureInfo? culture = null)
    {
        var reader = culture ?? CultureInfo.CurrentCulture;
        var local = createdUtc.ToLocalTime();

        return $"{local.ToString(DatePatternWithoutWeekday(reader), reader)}, {local.ToString("t", reader)}";
    }

    private static string DatePatternWithoutWeekday(CultureInfo culture) =>
        culture.DateTimeFormat.LongDatePattern
            .Replace("dddd", string.Empty, StringComparison.Ordinal)
            .Replace("ddd", string.Empty, StringComparison.Ordinal)
            .Trim(' ', ',', '،', '、')
            .Replace("  ", " ", StringComparison.Ordinal);
}
