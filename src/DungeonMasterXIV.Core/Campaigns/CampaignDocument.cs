using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// The whole campaign store as it sits on disk: a schema version and the campaigns.
/// </summary>
/// <remarks>
/// One file holding every campaign, rather than a file each, so that deleting a campaign is a
/// rewrite of this document with that campaign absent. A per-campaign file would leave the
/// deleted file's bytes to be unlinked separately and gives A-1.10 ("no trace remains") more ways
/// to be half-true.
/// </remarks>
public sealed class CampaignDocument
{
    /// <summary>
    /// The schema version this build writes. Bump it when the shape changes in a way that a
    /// document already on a DM's disk would not survive being read as-is.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The schema version of this document. Stamped immediately before every write, so it records
    /// the shape that was actually written rather than the shape that happened to be loaded.
    /// </summary>
    public int Version { get; set; } = CurrentSchemaVersion;

    /// <summary>Every campaign this machine holds.</summary>
    public List<Campaign> Campaigns { get; set; } = new();
}
