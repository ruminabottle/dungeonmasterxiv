namespace DungeonMasterXIV.Campaigns;

/// <summary>What loading campaigns found: nothing at all, at least one campaign, or only unreadable files.</summary>
public enum CampaignLoadOutcome
{
    FirstRun,

    Loaded,

    Unreadable,
}
