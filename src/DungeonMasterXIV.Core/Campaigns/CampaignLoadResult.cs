using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>What loading campaigns produced: the campaigns, unreadable files, outcome and migration counts.</summary>
public sealed class CampaignLoadResult
{
    public List<Campaign> Campaigns { get; } = new();

    public List<UnreadableCampaignFile> Unreadable { get; } = new();

    public CampaignLoadOutcome Outcome { get; set; } = CampaignLoadOutcome.FirstRun;

    public int Migrated { get; set; }

    public bool MigrationIncomplete { get; set; }
}
