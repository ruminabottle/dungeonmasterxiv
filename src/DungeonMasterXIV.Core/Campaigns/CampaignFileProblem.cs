namespace DungeonMasterXIV.Campaigns;

/// <summary>Why a file on disk is listed but cannot be shown as a campaign.</summary>
public enum CampaignFileProblem
{
    /// <summary>
    /// A campaign file this build cannot read — malformed, or written by a newer build whose shape
    /// it does not know. It is left exactly as it is rather than overwritten.
    /// </summary>
    WillNotParse,

    /// <summary>
    /// A file an earlier build left in the folder: the old single-file store when it could not be
    /// migrated, or a document a previous version preserved. It may hold participant labels.
    /// </summary>
    LeftByAnEarlierBuild,
}
