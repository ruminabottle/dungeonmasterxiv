using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Data;

/// <summary>
/// The DM's retained session logs: kept automatically, and <b>deletable from the place the product
/// says everything is deletable</b> (R-2.12, A-2.21).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DELETE CONTROL IS NOT A FOLLOW-UP, AND THE REASON IS A SENTENCE ALREADY ON SCREEN.</b>
/// <c>ConfigWindow</c> ships, today, <i>"Campaign history stays on the DM's machine. There is no
/// account, no server storing your sessions, and nothing to delete anywhere but here."</i> A
/// retained log that no control can reach <b>makes that sentence false</b> — which is the D-8
/// overclaim R-1.7a exists to prevent, and it fails by making live copy false rather than by
/// omitting a feature.
/// </para>
/// <para>
/// <b>THE SENTENCE IS NOT MINE TO CHANGE.</b> <c>ConfigWindow.cs</c> carries the note <i>"R-1.7a,
/// verbatim … If this needs to change, R-1.7a changes first."</i> So building the control is
/// engineering; altering the wording is the Spec Owner's. This type exists so the sentence can stay
/// exactly as it is.
/// </para>
/// <para>
/// <b>Deletion is keyed on the campaign because that is what the control deletes.</b> The existing
/// path is <c>CampaignStore.Delete(campaignId)</c>, reached from the settings list; a log carries
/// its campaign id for no other reason than to be reachable by it. That is also why logs are stored
/// beside campaigns rather than inside them — same control, separate data.
/// </para>
/// </remarks>
public sealed class RetainedLogStore(IRetainedLogArchive archive)
{
    private readonly IRetainedLogArchive _archive =
        archive ?? throw new ArgumentNullException(nameof(archive));

    /// <summary>
    /// Keeps <paramref name="log"/>, but <b>only when this client is the one that retains</b>.
    /// </summary>
    /// <param name="log">The session's log.</param>
    /// <param name="isHosting">Whether this client hosted — the DM's side.</param>
    /// <returns>True when it was written.</returns>
    /// <remarks>
    /// <b>The retention rule is asked, not assumed</b> (A-2.22). A player's log dies with the
    /// session unless exported, so a non-hosting client writes nothing at all — not written-then-
    /// cleaned-up, which would satisfy the sentence while leaving the file on disk in between.
    /// </remarks>
    public bool Retain(RetainedLog log, bool isHosting)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (!LogRetention.KeepsItsLog(isHosting))
        {
            return false;
        }

        _archive.Write(log.CampaignId, LogExport.Write(log));
        return true;
    }

    /// <summary>Whether a log is retained for <paramref name="campaignId"/>.</summary>
    public bool Has(Guid campaignId) => _archive.Read(campaignId) is not null;

    /// <summary>The retained log for <paramref name="campaignId"/> as written, or null.</summary>
    public string? Read(Guid campaignId) => _archive.Read(campaignId);

    /// <summary>Every campaign with a retained log.</summary>
    public IReadOnlyList<Guid> Retained() => _archive.Campaigns().ToList();

    /// <summary>
    /// Deletes the retained log for <paramref name="campaignId"/>. <b>Wired into the same control
    /// that deletes the campaign</b>, so the shipped sentence stays true.
    /// </summary>
    /// <returns>True when there was a log and it is now gone.</returns>
    public bool DeleteFor(Guid campaignId) => _archive.Delete(campaignId);
}
