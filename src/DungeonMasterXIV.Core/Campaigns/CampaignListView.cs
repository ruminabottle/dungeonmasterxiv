using System.Collections.Generic;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Builds the rows and detail text the campaign list shows for campaigns and for unreadable files.</summary>
public static class CampaignListView
{
    public const string NoCodeLabel = "(no code yet)";

    public static IReadOnlyList<CampaignRow> Build(IReadOnlyList<Campaign> campaigns)
    {
        var rows = new List<CampaignRow>(campaigns.Count);

        foreach (var campaign in campaigns)
        {
            rows.Add(new CampaignRow(campaign.CampaignId, Label(campaign), Detail(campaign)));
        }

        return rows;
    }

    public const string WillNotParseDetail =
        "This file cannot be read, so its campaign cannot be shown. It has been left exactly as it " +
        "is rather than overwritten. It may still contain participant names.";

    public const string LeftBehindDetail =
        "Left by an earlier version of the plugin. It is not used any more and may still contain " +
        "participant names.";

    public static IReadOnlyList<UnreadableRow> BuildUnreadable(IReadOnlyList<UnreadableCampaignFile> files)
    {
        var rows = new List<UnreadableRow>(files.Count);

        foreach (var file in files)
        {
            rows.Add(new UnreadableRow(file.FileName, DetailFor(file.Problem)));
        }

        return rows;
    }

    public const string StillHoldsCampaignsDetail =
        "This is the previous store, and it has been KEPT ON PURPOSE: one or more campaigns in it " +
        "could not be moved into files of their own, so this is the only copy of them. Deleting it " +
        "will lose those campaigns. The plugin will try again next time it loads.";

    private static string DetailFor(CampaignFileProblem problem) => problem switch
    {
        CampaignFileProblem.WillNotParse => WillNotParseDetail,
        CampaignFileProblem.StillHoldsCampaigns => StillHoldsCampaignsDetail,
        _ => LeftBehindDetail,
    };

    private static string Label(Campaign campaign) => CampaignName.For(campaign);

    private static string Detail(Campaign campaign)
    {
        var participants = campaign.Participants.Count == 1 ? "1 participant" : $"{campaign.Participants.Count} participants";
        return $"{participants} · started {campaign.CreatedUtc.UtcDateTime:yyyy-MM-dd}";
    }
}
