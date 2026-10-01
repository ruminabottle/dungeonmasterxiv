namespace DungeonMasterXIV.Campaigns;

/// <summary>A file in the campaign folder that could not be used as a campaign, with the reason.</summary>
public readonly record struct UnreadableCampaignFile(string FileName, CampaignFileProblem Problem);
