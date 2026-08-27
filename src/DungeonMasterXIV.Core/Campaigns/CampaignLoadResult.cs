using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>What a load of the campaign folder produced.</summary>
public sealed class CampaignLoadResult
{
    /// <summary>Campaigns that read cleanly, oldest first.</summary>
    public List<Campaign> Campaigns { get; } = new();

    /// <summary>Files that must still be listed and deletable, though they are not campaigns.</summary>
    public List<UnreadableCampaignFile> Unreadable { get; } = new();

    /// <summary>Whether anything was stored at all, and whether it read.</summary>
    public CampaignLoadOutcome Outcome { get; set; } = CampaignLoadOutcome.FirstRun;

    /// <summary>
    /// How many campaigns were moved off the old single-file store on this load. Counted from the
    /// files that were written, never from the campaigns that were read.
    /// </summary>
    public int Migrated { get; set; }

    /// <summary>
    /// Whether the old store still holds campaigns that could not be moved. When true it has been
    /// kept deliberately and is the only copy of those campaigns.
    /// </summary>
    public bool MigrationIncomplete { get; set; }
}
