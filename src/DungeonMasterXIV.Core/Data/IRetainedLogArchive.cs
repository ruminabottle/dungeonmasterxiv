using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Where retained logs live. Separated from the store so deletion can be tested without a disk.
/// </summary>
/// <remarks>
/// <b>Deliberately NOT <c>ICampaignArchive</c>.</b> R-2.12 draws the line that a roll log is not
/// campaign data — a campaign persists a roster, which is metadata, while a log is what people said
/// and did. Sharing the archive would put logs inside the campaign store by the back door, and the
/// requirement says explicitly that moving them there is <b>a product decision, not a storage
/// one</b>. Two archives, one delete control.
/// </remarks>
public interface IRetainedLogArchive
{
    /// <summary>Every campaign id that currently has a retained log.</summary>
    IReadOnlyList<Guid> Campaigns();

    /// <summary>Reads a campaign's log, or null when there is none.</summary>
    string? Read(Guid campaignId);

    /// <summary>Writes a campaign's log, replacing any previous one.</summary>
    void Write(Guid campaignId, string contents);

    /// <summary>Deletes a campaign's log. Returns whether there was one to delete.</summary>
    bool Delete(Guid campaignId);
}
