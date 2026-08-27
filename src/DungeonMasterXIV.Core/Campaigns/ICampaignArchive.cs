using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Where campaign files are kept: one file per campaign (A-1.11b), plus whatever older files are
/// still lying in the same folder.
/// </summary>
/// <remarks>
/// This port carries no policy. It does not know what a campaign is, what makes a file unreadable,
/// or when to migrate — those decisions live in <see cref="CampaignStoreLoader"/> and
/// <see cref="CampaignStore"/>. It knows names and bytes.
/// </remarks>
public interface ICampaignArchive
{
    /// <summary>Every campaign file present, by name, in a stable order.</summary>
    IReadOnlyList<string> CampaignFiles();

    /// <summary>The contents of one campaign file, or <c>null</c> if it is not there.</summary>
    /// <param name="name">A campaign file name.</param>
    string? ReadCampaign(string name);

    /// <summary>Writes one campaign file, replacing it if it exists.</summary>
    /// <param name="name">A campaign file name.</param>
    /// <param name="contents">The serialized campaign file.</param>
    void WriteCampaign(string name, string contents);

    /// <summary>Removes one file this plugin owns — a campaign file, a legacy file, or a preserved one.</summary>
    /// <param name="name">The file's name.</param>
    /// <returns>Whether a file was removed.</returns>
    bool Delete(string name);

    /// <summary>
    /// The old single-file store's contents, or <c>null</c> if it is not there. Read once so it can
    /// be migrated onto the per-campaign layout, then deleted. Never written.
    /// </summary>
    string? ReadLegacy();

    /// <summary>
    /// Files this plugin left behind that are not campaign files — the legacy store when it is
    /// still present, and anything preserved by an older build. They may hold participant labels,
    /// which is why A-1.10 requires the DM be able to see and delete them.
    /// </summary>
    IReadOnlyList<string> OtherOwnedFiles();
}
