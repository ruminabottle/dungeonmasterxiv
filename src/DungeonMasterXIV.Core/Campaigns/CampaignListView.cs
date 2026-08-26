using System.Collections.Generic;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Builds the campaign list's display rows.
/// </summary>
/// <remarks>
/// Here rather than in the window because a draw callback may not allocate per frame, so the
/// window builds rows once and redraws them — and because doing the formatting in a plain type
/// means the wording is unit-tested instead of only ever being looked at.
/// </remarks>
public static class CampaignListView
{
    /// <summary>Shown for a campaign that has never been hosted and so has no code yet.</summary>
    public const string NoCodeLabel = "(no code yet)";

    /// <summary>
    /// Turns campaigns into rows. A campaign is labelled by its preferred code because R-1.2a
    /// makes the code a campaign's default label — the row still acts on the campaign's UUID, so
    /// two campaigns sharing a preferred code remain separately addressable.
    /// </summary>
    /// <param name="campaigns">The campaigns to show.</param>
    public static IReadOnlyList<CampaignRow> Build(IReadOnlyList<Campaign> campaigns)
    {
        var rows = new List<CampaignRow>(campaigns.Count);

        foreach (var campaign in campaigns)
        {
            rows.Add(new CampaignRow(campaign.CampaignId, Label(campaign), Detail(campaign)));
        }

        return rows;
    }

    private static string Label(Campaign campaign) =>
        SessionCode.TryParse(campaign.PreferredCode, out var code) ? code.ToDisplayString() : NoCodeLabel;

    private static string Detail(Campaign campaign)
    {
        var participants = campaign.Participants.Count == 1 ? "1 participant" : $"{campaign.Participants.Count} participants";
        return $"{participants} · started {campaign.CreatedUtc.UtcDateTime:yyyy-MM-dd}";
    }
}
