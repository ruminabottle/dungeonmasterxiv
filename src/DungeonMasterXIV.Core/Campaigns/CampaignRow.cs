namespace DungeonMasterXIV.Campaigns;

/// <summary>One campaign's row in the campaign list: its identifier, label and detail text.</summary>
public readonly record struct CampaignRow(System.Guid CampaignId, string Label, string Detail);

/// <summary>One unreadable file's row in the campaign list: its file name and an explanation.</summary>
public readonly record struct UnreadableRow(string FileName, string Detail);
