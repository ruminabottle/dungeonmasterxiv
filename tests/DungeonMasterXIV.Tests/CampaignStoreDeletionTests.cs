using System;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Deletion (A-1.10) and the D-8 promise about what we write down.
/// </summary>
public class CampaignStoreDeletionTests
{
    private const string Label = "Yshtola Rhul";

    [Fact]
    public void DeletingACampaignLeavesNoTraceOfItInWhatIsWritten()
    {
        // Asserted both ways round on purpose. "The deleted campaign is absent" is satisfied by a
        // store that writes an empty document, so the surviving campaign must be positively
        // present in the same assertion — otherwise the check cannot fail in the way that matters.
        var archive = new FakeCampaignArchive();
        var store = new CampaignStore(archive, new RecordingCampaignLog());

        var doomed = store.Create(SessionCode.FromValid("BKD7RM"));
        var survivor = store.Create(SessionCode.FromValid("XW2P4N"));
        var doomedParticipant = store.AddParticipant(doomed.CampaignId, Label)!.ParticipantId;
        var survivingParticipant = store.AddParticipant(survivor.CampaignId, Label)!.ParticipantId;

        Assert.True(store.Delete(doomed.CampaignId));

        var written = archive.Content;
        Assert.NotNull(written);
        Assert.DoesNotContain(doomed.CampaignId.ToString(), written!);
        Assert.DoesNotContain(doomedParticipant.ToString(), written!);
        Assert.Contains(survivor.CampaignId.ToString(), written!);
        Assert.Contains(survivingParticipant.ToString(), written!);
    }

    [Fact]
    public void ADeletedCampaignIsGoneFromAStoreLoadedAfterwards()
    {
        // The in-memory list and the file agreeing is the point: a delete that only filtered the
        // view would pass an in-memory assertion and fail this one.
        var archive = new FakeCampaignArchive();
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        var doomed = store.Create(SessionCode.FromValid("BKD7RM"));
        store.Create(SessionCode.FromValid("XW2P4N"));
        store.AddParticipant(doomed.CampaignId, Label);

        store.Delete(doomed.CampaignId);
        var reloaded = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(CampaignLoadOutcome.Loaded, reloaded.LoadOutcome);
        Assert.Single(reloaded.Campaigns);
        Assert.Null(reloaded.Find(doomed.CampaignId));
    }

    [Fact]
    public void DeletingSomethingThatIsNotThereWritesNothing()
    {
        var archive = new FakeCampaignArchive();
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        store.Create(SessionCode.FromValid("BKD7RM"));
        var writesBefore = archive.Writes.Count;

        Assert.False(store.Delete(Guid.NewGuid()));
        Assert.Equal(writesBefore, archive.Writes.Count);
    }

    [Fact]
    public void NoLogLineEverCarriesAParticipantLabel()
    {
        // D-8: a character name may live in the DM's local store and may not reach a line we
        // write. Log the campaign's own id instead. The non-empty assertion matters — a store
        // that logged nothing at all would otherwise pass this trivially.
        var log = new RecordingCampaignLog();
        var store = new CampaignStore(new FakeCampaignArchive(), log);
        var campaign = store.Create(SessionCode.FromValid("BKD7RM"));
        store.AddParticipant(campaign.CampaignId, Label);

        store.Delete(campaign.CampaignId);

        Assert.NotEmpty(log.AllLines);
        Assert.All(log.AllLines, line => Assert.DoesNotContain(Label, line, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryWriteStampsTheVersionThisBuildWrites()
    {
        var archive = new FakeCampaignArchive();
        var store = new CampaignStore(archive, new RecordingCampaignLog());

        store.Create(SessionCode.FromValid("BKD7RM"));

        Assert.True(CampaignDocumentCodec.TryDeserialize(archive.Content!, out var written));
        Assert.Equal(CampaignDocument.CurrentSchemaVersion, written!.Version);
    }

    [Fact]
    public void EveryWriteMovesTheRevisionSoTheDrawPathKnowsToRebuild()
    {
        var store = new CampaignStore(new FakeCampaignArchive(), new RecordingCampaignLog());
        var before = store.Revision;

        var campaign = store.Create(SessionCode.FromValid("BKD7RM"));
        var afterCreate = store.Revision;
        store.Delete(campaign.CampaignId);

        Assert.NotEqual(before, afterCreate);
        Assert.NotEqual(afterCreate, store.Revision);
    }
}
