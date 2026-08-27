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

    /// <summary>Copy for a campaign file that will not parse.</summary>
    public const string WillNotParseDetail =
        "This file cannot be read, so its campaign cannot be shown. It has been left exactly as it " +
        "is rather than overwritten. It may still contain participant names.";

    /// <summary>Copy for a file an earlier build left in the folder.</summary>
    public const string LeftBehindDetail =
        "Left by an earlier version of the plugin. It is not used any more and may still contain " +
        "participant names.";

    /// <summary>
    /// Turns unreadable files into rows. These are listed for the same reason campaigns are:
    /// A-1.10 requires the DM can see and delete everything the machine holds, and a file that
    /// cannot be read is the one they can least reason about.
    /// </summary>
    /// <param name="files">The files that would not read.</param>
    public static IReadOnlyList<UnreadableRow> BuildUnreadable(IReadOnlyList<UnreadableCampaignFile> files)
    {
        var rows = new List<UnreadableRow>(files.Count);

        foreach (var file in files)
        {
            rows.Add(new UnreadableRow(file.FileName, DetailFor(file.Problem)));
        }

        return rows;
    }

    /// <summary>Copy for the previous store when it is still the only copy of some campaigns.</summary>
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

    private static string Label(Campaign campaign) =>
        SessionCode.TryParse(campaign.PreferredCode, out var code) ? code.ToDisplayString() : NoCodeLabel;

    private static string Detail(Campaign campaign)
    {
        var participants = campaign.Participants.Count == 1 ? "1 participant" : $"{campaign.Participants.Count} participants";
        return $"{participants} · started {campaign.CreatedUtc.UtcDateTime:yyyy-MM-dd}";
    }
}
