using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Loads campaigns from an archive, then creates, changes, saves and deletes them, counting changes.</summary>
public sealed class CampaignStore
{
    private readonly ICampaignArchive _archive;
    private readonly ICampaignStoreLog _log;
    private readonly List<Campaign> _campaigns;
    private readonly List<UnreadableCampaignFile> _unreadable;

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

    public IReadOnlyList<Campaign> Campaigns => _campaigns;

    public IReadOnlyList<UnreadableCampaignFile> Unreadable => _unreadable;

    public CampaignLoadOutcome LoadOutcome { get; }

    public int Migrated { get; }

    public int Revision { get; private set; }

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

    public Campaign? Find(Guid campaignId) =>
        _campaigns.FirstOrDefault(campaign => campaign.CampaignId == campaignId);

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

    public void Save(Campaign campaign)
    {
        _archive.WriteCampaign(CampaignFileName.NameFor(campaign.CampaignId), CampaignFileCodec.Serialize(campaign));
        Revision++;
    }
}
