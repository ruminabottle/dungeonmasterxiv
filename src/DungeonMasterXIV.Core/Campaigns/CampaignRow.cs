namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// One campaign as the campaign list draws it: strings already built, nothing left to format.
/// </summary>
/// <param name="CampaignId">Identifies the campaign a delete button acts on.</param>
/// <param name="Label">The campaign's display label — its preferred code, or a stand-in.</param>
/// <param name="Detail">The secondary line: participant count and creation date.</param>
public readonly record struct CampaignRow(System.Guid CampaignId, string Label, string Detail);

/// <summary>
/// One file the DM can see and delete but not open as a campaign.
/// </summary>
/// <param name="FileName">The bare file name, which is also what deletes it.</param>
/// <param name="Detail">Plain words for why it is listed here.</param>
public readonly record struct UnreadableRow(string FileName, string Detail);
