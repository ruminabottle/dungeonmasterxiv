namespace DungeonMasterXIV.Campaigns;

public readonly record struct CampaignRow(System.Guid CampaignId, string Label, string Detail);

public readonly record struct UnreadableRow(string FileName, string Detail);
