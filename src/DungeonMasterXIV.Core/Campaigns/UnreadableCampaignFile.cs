namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// A file the DM must be able to see and delete even though it cannot be shown as a campaign
/// (A-1.10, extended 2026-08-27 to cover files the plugin cannot read or parse).
/// </summary>
/// <remarks>
/// An unreadable file is exactly the one a user cannot reason about, so it is the one that most
/// needs to be visible. These sit in the same folder people zip into a bug report and may hold
/// participant labels.
/// </remarks>
/// <param name="FileName">The bare file name, which is also what deletes it.</param>
/// <param name="Problem">Why it is here rather than in the campaign list.</param>
public readonly record struct UnreadableCampaignFile(string FileName, CampaignFileProblem Problem);
