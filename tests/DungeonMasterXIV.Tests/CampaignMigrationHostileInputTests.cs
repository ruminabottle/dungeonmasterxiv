using System;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Migration against v1 stores the previous build's writer could never have produced.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these are not built with the real writer, when the sibling tests deliberately are.</b>
/// Building the fixture from the previous build's own writer proves the format is real, and it has
/// a ceiling: <b>it cannot reach any state the old code refused to create.</b> <c>Create</c> used a
/// fresh UUID, so no writer of ours ever emitted two campaigns sharing an id — and the migration
/// destroyed one of them.
/// </para>
/// <para>
/// A v1 file arrives from a hand-edited file, a restored backup, or two machines' folders merged.
/// So these fixtures are deliberately valid-but-weird rather than malformed: duplicate ids, an
/// empty campaign list, a label carrying a path separator or a newline. That is the state space a
/// real-writer fixture cannot cover, and the reason the two styles sit in separate files.
/// </para>
/// </remarks>
public class CampaignMigrationHostileInputTests
{
    private static readonly Guid Shared = new("2f1d5b8e-0000-4000-8000-000000000001");

    private static string AV1StoreWithTwoCampaignsSharingAnId()
    {
        // Written through the real v1 codec so the SHAPE is genuine; the duplicate id is the part
        // no writer of ours would have produced.
        var document = new CampaignDocument();
        document.Campaigns.Add(new Campaign
        {
            CampaignId = Shared,
            PreferredCode = "BKD7RM",
            Participants = { new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = "Yshtola Rhul" } },
        });
        document.Campaigns.Add(new Campaign
        {
            CampaignId = Shared,
            PreferredCode = "XW2P4N",
            Participants = { new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = "Thancred Waters" } },
        });

        return CampaignDocumentCodec.Serialize(document);
    }

    // THE BLOCKING FINDING. Before the fix: the second campaign overwrote the first, the legacy file
    // was deleted unconditionally, and one campaign was GONE rather than merely unlisted -- while
    // the log said "Moved 2 campaign(s)".
    [Fact]
    public void TwoCampaignsSharingAnIdDoNotDestroyEachOtherAndTheOldFileIsKept()
    {
        var v1 = AV1StoreWithTwoCampaignsSharingAnId();
        var archive = new FakeCampaignArchive(v1);
        var log = new RecordingCampaignLog();

        var store = new CampaignStore(archive, log);

        // The old file is the only remaining copy of the campaign that could not be moved, so it
        // must still be there.
        Assert.Equal(v1, archive.ReadLegacy());
        Assert.Contains(log.Warnings, line => line.Contains("share an identifier"));
    }

    // The count must be derived from what LANDED. Counting the input campaigns produces a number
    // that structurally cannot report a failure to write -- it would say "moved 2" over one file.
    [Fact]
    public void TheMigratedCountReportsFilesWrittenRatherThanCampaignsRead()
    {
        var archive = new FakeCampaignArchive(AV1StoreWithTwoCampaignsSharingAnId());

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(1, store.Migrated);
        Assert.Equal(1, archive.Files.Keys.Count(CampaignFileName.IsCampaignFileName));
        Assert.Equal(store.Migrated, archive.Files.Keys.Count(CampaignFileName.IsCampaignFileName));
    }

    // The retained store must not be described as unused -- it is the only copy of a campaign, and
    // "not used any more" is the sentence that would cost a DM their data.
    [Fact]
    public void ARetainedStoreIsDescribedAsStillHoldingCampaigns()
    {
        var archive = new FakeCampaignArchive(AV1StoreWithTwoCampaignsSharingAnId());
        var store = new CampaignStore(archive, new RecordingCampaignLog());

        var entry = Assert.Single(store.Unreadable, e => e.FileName == CampaignFileName.LegacyFileName);
        Assert.Equal(CampaignFileProblem.StillHoldsCampaigns, entry.Problem);

        var row = Assert.Single(CampaignListView.BuildUnreadable(store.Unreadable), r => r.FileName == entry.FileName);
        Assert.Equal(CampaignListView.StillHoldsCampaignsDetail, row.Detail);
        Assert.DoesNotContain("not used any more", row.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // THE SECOND BLOCKING FINDING. This path runs once for every existing user, on upgrade, inside
    // the store's constructor. A transient write failure must not stop the plugin loading.
    [Fact]
    public void AWriteFailureDuringMigrationDoesNotStopThePluginLoading()
    {
        var document = new CampaignDocument();
        var first = Guid.NewGuid();
        document.Campaigns.Add(new Campaign { CampaignId = first, PreferredCode = "BKD7RM" });
        var v1 = CampaignDocumentCodec.Serialize(document);
        var archive = new FakeCampaignArchive(v1) { FailWriteForName = CampaignFileName.NameFor(first) };
        var log = new RecordingCampaignLog();

        var store = new CampaignStore(archive, log);

        Assert.Equal(0, store.Migrated);
        Assert.Equal(v1, archive.ReadLegacy());
        Assert.Contains(log.Warnings, line => line.Contains("retried on the next load"));
    }

    [Fact]
    public void TheMigrationRetriesAndCompletesOnceTheDiskRecovers()
    {
        var document = new CampaignDocument();
        var first = Guid.NewGuid();
        document.Campaigns.Add(new Campaign { CampaignId = first, PreferredCode = "BKD7RM" });
        var archive = new FakeCampaignArchive(CampaignDocumentCodec.Serialize(document))
        {
            FailWriteForName = CampaignFileName.NameFor(first),
        };
        _ = new CampaignStore(archive, new RecordingCampaignLog());

        archive.FailWriteForName = null;
        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(1, store.Migrated);
        Assert.Single(store.Campaigns);
        Assert.Null(archive.ReadLegacy());
    }

    [Fact]
    public void AnEmptyV1StoreMigratesToNothingAndIsRemoved()
    {
        var archive = new FakeCampaignArchive(CampaignDocumentCodec.Serialize(new CampaignDocument()));

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(0, store.Migrated);
        Assert.Empty(store.Campaigns);
        Assert.Null(archive.ReadLegacy());
    }

    // A label is DM-authored text and may contain anything. It must never influence a file name.
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("a label\nwith a newline")]
    [InlineData("a\\backslash")]
    public void AHostileLabelDoesNotReachAFileName(string label)
    {
        var campaignId = Guid.NewGuid();
        var document = new CampaignDocument();
        document.Campaigns.Add(new Campaign
        {
            CampaignId = campaignId,
            Participants = { new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = label } },
        });
        var archive = new FakeCampaignArchive(CampaignDocumentCodec.Serialize(document));

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(new[] { CampaignFileName.NameFor(campaignId) }, archive.Files.Keys.ToArray());
        Assert.Equal(label, Assert.Single(Assert.Single(store.Campaigns).Participants).Label);
    }
}
