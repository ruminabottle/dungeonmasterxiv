using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// The DM's campaigns, on the DM's machine (R-1.6). Lists them, creates them, remembers who has
/// played in them, and deletes one outright.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every lookup here takes a <see cref="Guid"/>.</b> R-1.2a makes a session code the label of a
/// live session rather than the name of a campaign — a DM whose usual code is taken at resume must
/// get a new code and the same campaign. A by-code lookup is how that guarantee would be lost, so
/// the shape of this type refuses it.
/// </para>
/// <para>
/// <b>One file per campaign</b> (A-1.11b). That bounds what a single file discloses when someone
/// attaches one to a bug report; it is <i>not</i> a claim to satisfy A-1.11, which since
/// 2026-08-27 is about what leaves the machine. Two files in one folder link a person exactly as
/// well as one did.
/// </para>
/// <para>Nothing here leaves the machine. This store is not the relay and does not soften D-2.</para>
/// </remarks>
public sealed class CampaignStore
{
    private readonly ICampaignArchive _archive;
    private readonly ICampaignStoreLog _log;
    private readonly List<Campaign> _campaigns;
    private readonly List<UnreadableCampaignFile> _unreadable;

    /// <param name="archive">Where campaign files are kept.</param>
    /// <param name="log">Where load outcomes are reported.</param>
    public CampaignStore(ICampaignArchive archive, ICampaignStoreLog log)
    {
        _archive = archive;
        _log = log;

        var loaded = CampaignStoreLoader.Load(archive, log);
        _campaigns = loaded.Campaigns;
        _unreadable = loaded.Unreadable;
        LoadOutcome = loaded.Outcome;
        Migrated = loaded.Migrated;
    }

    /// <summary>Every campaign this machine holds and can read.</summary>
    public IReadOnlyList<Campaign> Campaigns => _campaigns;

    /// <summary>
    /// Files that are on disk and are not readable campaigns. A-1.10 requires the DM can list and
    /// delete these too: an unreadable file is the one a user cannot reason about, and it may hold
    /// participant labels.
    /// </summary>
    public IReadOnlyList<UnreadableCampaignFile> Unreadable => _unreadable;

    /// <summary>Whether anything was stored at all, and whether any of it read.</summary>
    public CampaignLoadOutcome LoadOutcome { get; }

    /// <summary>How many campaigns were moved off the old single-file store on this load.</summary>
    public int Migrated { get; }

    /// <summary>
    /// Increments on every change. A draw callback may not rebuild its display rows each frame, so
    /// the campaign list caches them and rebuilds only when this changes.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Starts a campaign and returns it. Its identity is generated here and never derived from
    /// <paramref name="preferredCode"/>.
    /// </summary>
    /// <param name="preferredCode">The code the DM likes for it, if it has been hosted yet.</param>
    public Campaign Create(SessionCode? preferredCode)
    {
        var campaign = new Campaign
        {
            CampaignId = Guid.NewGuid(),
            PreferredCode = preferredCode?.Value,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        _campaigns.Add(campaign);
        Save(campaign);
        return campaign;
    }

    /// <summary>The campaign with this identity, or <c>null</c>.</summary>
    /// <param name="campaignId">The campaign's local UUID.</param>
    public Campaign? Find(Guid campaignId) =>
        _campaigns.FirstOrDefault(campaign => campaign.CampaignId == campaignId);

    /// <summary>
    /// Records a participant in a campaign and returns them, with a UUID generated fresh for this
    /// campaign, so the same person in another campaign gets an unrelated identifier.
    /// <para>
    /// <b>That rotation is necessary for A-1.11 and not sufficient for it.</b> The label is not
    /// rotated; see <see cref="CampaignParticipant"/> for what is and is not guaranteed.
    /// </para>
    /// </summary>
    /// <param name="campaignId">The campaign they played in.</param>
    /// <param name="label">What the DM calls them locally. May be a character name; never logged.</param>
    /// <remarks>
    /// <b>UNCALLED IN PRODUCTION ON PURPOSE, and that is A-1.9m rather than an oversight.</b> An
    /// empty roster is the CORRECT state today: this appends unconditionally with a fresh id, and
    /// there is no durable joiner identity to de-duplicate on — the joiner is never told its
    /// <see cref="CampaignParticipant.ParticipantId"/>, its keys are regenerated every join, and its
    /// peer code is derived from two per-session inputs. So wiring this to admission would record
    /// one person once per session and <see cref="Save"/> the result, putting phantom participants
    /// on the DM's disk that no later migration can disentangle.
    /// <para>
    /// It becomes callable when a returning client can present a claim — R-1.5c's conveyance, then
    /// the joiner storing it. <b>Until then this looks exactly like an oversight, which is why the
    /// prohibition is a criterion and this remark exists.</b>
    /// </para>
    /// </remarks>
    public CampaignParticipant? AddParticipant(Guid campaignId, string label)
    {
        var campaign = Find(campaignId);
        if (campaign is null)
        {
            return null;
        }

        var participant = new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = label };
        campaign.Participants.Add(participant);
        Save(campaign);
        return participant;
    }

    /// <summary>
    /// Changes which code a campaign prefers. Its identity is untouched: a code taken at resume
    /// costs a new code, not the campaign (R-1.2a).
    /// </summary>
    /// <param name="campaignId">The campaign to relabel.</param>
    /// <param name="preferredCode">The code it should now ask the relay for.</param>
    public bool SetPreferredCode(Guid campaignId, SessionCode preferredCode)
    {
        var campaign = Find(campaignId);
        if (campaign is null)
        {
            return false;
        }

        campaign.PreferredCode = preferredCode.Value;
        Save(campaign);
        return true;
    }

    /// <summary>
    /// Deletes a campaign outright, removing its file, so no participant, UUID or state of that
    /// campaign survives on disk (A-1.10).
    /// </summary>
    /// <param name="campaignId">The campaign to delete.</param>
    public bool Delete(Guid campaignId)
    {
        var campaign = Find(campaignId);
        if (campaign is null)
        {
            return false;
        }

        var participantCount = campaign.Participants.Count;
        _campaigns.Remove(campaign);
        _archive.Delete(CampaignFileName.NameFor(campaignId));
        Revision++;
        _log.Information($"Deleted campaign {campaignId} and its {participantCount} participant record(s).");
        return true;
    }

    /// <summary>
    /// Deletes a file that is on disk but is not a readable campaign. The other half of A-1.10:
    /// preserving what will not parse is only useful if the DM can subsequently be rid of it.
    /// </summary>
    /// <param name="fileName">A name from <see cref="Unreadable"/>.</param>
    public bool DeleteUnreadable(string fileName)
    {
        var index = _unreadable.FindIndex(entry => entry.FileName == fileName);
        if (index < 0)
        {
            _log.Warning($"Refused to delete '{fileName}': it is not a file this store is holding.");
            return false;
        }

        if (!_archive.Delete(fileName))
        {
            _log.Warning($"Could not delete '{fileName}'.");
            return false;
        }

        _unreadable.RemoveAt(index);
        Revision++;
        _log.Information($"Deleted unreadable campaign file {fileName}.");
        return true;
    }

    /// <summary>Writes one campaign's file, stamped with the schema version it is written in.</summary>
    /// <param name="campaign">The campaign to persist.</param>
    public void Save(Campaign campaign)
    {
        _archive.WriteCampaign(CampaignFileName.NameFor(campaign.CampaignId), CampaignFileCodec.Serialize(campaign));
        Revision++;
    }
}
