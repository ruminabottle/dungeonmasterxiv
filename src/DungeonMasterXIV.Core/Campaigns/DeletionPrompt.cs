using System;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Which row, if any, has been asked to delete and is waiting to be confirmed.
/// </summary>
/// <remarks>
/// <para>
/// A campaign is named by its id and an unreadable file only by its name, and the confirmation this
/// replaced was a <c>Guid?</c> — so it could represent the first and not the second, and the row it
/// could not represent is the one that deleted on a single click. The state is widened here rather
/// than duplicated: a second flag alongside the first would let two rows be pending at once and
/// would drift apart, which is the same defect one level down.
/// </para>
/// <para>
/// One nullable field holds the pending row, so "at most one row is awaiting confirmation" is
/// structural rather than something the callers have to maintain. Requesting a second row replaces
/// the first, which is what stops a cancelled or abandoned prompt confirming a deletion the user has
/// since navigated away from.
/// </para>
/// </remarks>
public sealed class DeletionPrompt
{
    private readonly record struct Target(Guid? CampaignId, string? FileName);

    private readonly Action<Guid> _deleteCampaign;
    private readonly Action<string> _deleteFile;

    private Target? _pending;

    /// <param name="deleteCampaign">Deletes a readable campaign by id.</param>
    /// <param name="deleteFile">Deletes an unreadable file by name.</param>
    /// <remarks>
    /// Both are taken once, at construction, because the caller is a draw callback: resolving them
    /// per row per frame would allocate a delegate in a loop that runs every frame.
    /// </remarks>
    public DeletionPrompt(Action<Guid> deleteCampaign, Action<string> deleteFile)
    {
        ArgumentNullException.ThrowIfNull(deleteCampaign);
        ArgumentNullException.ThrowIfNull(deleteFile);

        _deleteCampaign = deleteCampaign;
        _deleteFile = deleteFile;
    }

    /// <summary>Whether this campaign is the row awaiting confirmation.</summary>
    public bool IsAwaiting(Guid campaignId) => _pending?.CampaignId == campaignId;

    /// <summary>Whether this unreadable file is the row awaiting confirmation.</summary>
    public bool IsAwaiting(string fileName) => _pending?.FileName == fileName;

    /// <summary>Asks to delete a campaign. Nothing is deleted until <see cref="Confirm"/>.</summary>
    public void Request(Guid campaignId) => _pending = new Target(campaignId, null);

    /// <summary>Asks to delete an unreadable file. Nothing is deleted until <see cref="Confirm"/>.</summary>
    public void Request(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _pending = new Target(null, fileName);
    }

    /// <summary>Abandons the pending request. The row is left exactly as it was.</summary>
    public void Cancel() => _pending = null;

    /// <summary>
    /// Deletes the pending row, if there is one, and clears the prompt. Does nothing when nothing is
    /// pending, so a confirm that arrives after a cancel cannot delete anything.
    /// </summary>
    public void Confirm()
    {
        if (_pending is not { } target)
        {
            return;
        }

        // Cleared before the delete runs, so a delete that throws cannot leave the row pending and
        // primed to fire again on the next frame.
        _pending = null;

        if (target.CampaignId is { } campaignId)
        {
            _deleteCampaign(campaignId);
        }
        else if (target.FileName is { } fileName)
        {
            _deleteFile(fileName);
        }
    }
}
