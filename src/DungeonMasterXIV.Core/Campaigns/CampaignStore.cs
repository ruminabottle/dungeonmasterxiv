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
/// <b>Every lookup here takes a <see cref="Guid"/>.</b> No method finds a campaign by its session
/// code, because R-1.2a makes a code the label of a live session rather than the name of a
/// campaign — a DM whose usual code is taken at resume must get a new code and the same campaign.
/// A by-code lookup is how that guarantee would be lost, so the shape of this type refuses it.
/// </para>
/// <para>
/// Nothing here leaves the machine. This store is not the relay and does not soften D-2 (E-9).
/// </para>
/// </remarks>
public sealed class CampaignStore
{
    private readonly ICampaignArchive _archive;
    private readonly ICampaignStoreLog _log;
    private readonly CampaignDocument _document;

    /// <param name="archive">Where the document is kept.</param>
    /// <param name="log">Where load outcomes are reported.</param>
    public CampaignStore(ICampaignArchive archive, ICampaignStoreLog log)
    {
        _archive = archive;
        _log = log;
        _document = Load(out var outcome, out var loadedVersion);
        LoadOutcome = outcome;
        LoadedVersion = loadedVersion;
    }

    /// <summary>Every campaign this machine holds, oldest first.</summary>
    public IReadOnlyList<Campaign> Campaigns => _document.Campaigns;

    /// <summary>How the document arrived: never stored, read, or preserved because it would not read.</summary>
    public CampaignLoadOutcome LoadOutcome { get; }

    /// <summary>
    /// Increments on every write. A draw callback may not rebuild its display rows each frame, so
    /// the campaign list caches them and rebuilds only when this changes. Conservative by design:
    /// a save that altered nothing still bumps it, which costs one rebuild and can never go stale.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// The schema version that came off disk, or <c>null</c> when nothing readable was loaded.
    /// A migration belongs here: this is the only point that knows which shape arrived, and the
    /// document's own version is restamped by the next save.
    /// </summary>
    public int? LoadedVersion { get; }

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

        _document.Campaigns.Add(campaign);
        Save();
        return campaign;
    }

    /// <summary>The campaign with this identity, or <c>null</c>.</summary>
    /// <param name="campaignId">The campaign's local UUID.</param>
    public Campaign? Find(Guid campaignId) =>
        _document.Campaigns.FirstOrDefault(campaign => campaign.CampaignId == campaignId);

    /// <summary>
    /// Records a participant in a campaign and returns them, with a UUID generated fresh for this
    /// campaign, so the same person in another campaign gets an unrelated identifier.
    /// <para>
    /// <b>That rotation is necessary for A-1.11 and not sufficient for it.</b> The label is not
    /// rotated, and it is the field most likely to match across campaigns; see
    /// <see cref="CampaignParticipant"/> for what is and is not guaranteed.
    /// </para>
    /// </summary>
    /// <param name="campaignId">The campaign they played in.</param>
    /// <param name="label">What the DM calls them locally. May be a character name; never logged.</param>
    public CampaignParticipant? AddParticipant(Guid campaignId, string label)
    {
        var campaign = Find(campaignId);
        if (campaign is null)
        {
            return null;
        }

        var participant = new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = label };
        campaign.Participants.Add(participant);
        Save();
        return participant;
    }

    /// <summary>
    /// Changes which code a campaign prefers. The campaign's identity is untouched, which is the
    /// whole point: a code taken at resume costs a new code, not the campaign (R-1.2a).
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
        Save();
        return true;
    }

    /// <summary>
    /// Deletes a campaign outright and rewrites the document without it, so no participant, UUID
    /// or state of that campaign survives the write (A-1.10).
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
        _document.Campaigns.Remove(campaign);
        Save();
        _log.Information(
            $"Deleted campaign {campaignId} and its {participantCount} participant record(s).");
        return true;
    }

    /// <summary>
    /// Unreadable documents kept aside and not yet removed. They still hold whatever the file held,
    /// participant labels included, so A-1.10's "no trace remains" is not true of a campaign whose
    /// data is sitting in one of these — which is why they are listable and deletable rather than
    /// merely preserved.
    /// </summary>
    public IReadOnlyList<string> PreservedFiles() => _archive.PreservedFiles();

    /// <summary>
    /// Deletes one preserved file. The name must be one from <see cref="PreservedFiles"/>; the
    /// archive refuses anything else.
    /// </summary>
    /// <param name="name">A name from <see cref="PreservedFiles"/>.</param>
    public bool DeletePreserved(string name)
    {
        // No name check here. The archive turns the name into a path and is the layer that guards
        // it — a copy of that check in this method could never fail, because nothing reaches the
        // archive except through here.
        if (!_archive.DeletePreserved(name))
        {
            _log.Warning($"Could not delete preserved campaign file '{name}'.");
            return false;
        }

        Revision++;
        _log.Information($"Deleted preserved campaign file {name}.");
        return true;
    }

    /// <summary>Writes the document, stamped with the schema version it is written in.</summary>
    public void Save()
    {
        _archive.Write(CampaignDocumentCodec.Serialize(_document));
        Revision++;
    }

    private CampaignDocument Load(out CampaignLoadOutcome outcome, out int? loadedVersion)
    {
        loadedVersion = null;
        var stored = _archive.Read();

        if (stored is null)
        {
            outcome = CampaignLoadOutcome.FirstRun;
            _log.Information("No campaign store found. This machine has not saved a campaign before.");
            return new CampaignDocument();
        }

        if (CampaignDocumentCodec.TryDeserialize(stored, out var document) && document is not null)
        {
            outcome = CampaignLoadOutcome.Loaded;
            loadedVersion = document.Version;
            _log.Information(
                $"Loaded {document.Campaigns.Count} campaign(s), schema version {document.Version}.");
            return document;
        }

        outcome = CampaignLoadOutcome.Unreadable;
        var keptAt = _archive.PreserveUnreadable();
        _log.Warning(
            $"The campaign store could not be read and has been kept at {keptAt} rather than " +
            "overwritten. Starting from an empty store; the campaigns in that file are not lost.");
        return new CampaignDocument();
    }
}
