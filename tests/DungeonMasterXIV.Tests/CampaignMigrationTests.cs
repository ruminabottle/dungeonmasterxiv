using System;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Migrating the pre-C10 single-file store onto the per-campaign layout A-1.11b requires.
/// </summary>
/// <remarks>
/// <b>The v1 fixture is produced by the previous build's own writer, not hand-authored.</b>
/// <c>CampaignDocumentCodec.Serialize</c> and <c>CampaignDocument</c> are byte-identical to the
/// code that shipped in PR #8 — verified with <c>git diff origin/main</c> — so the bytes below are
/// what a DM's disk actually holds. A hand-written JSON literal would encode my belief about the
/// old format, and since the same belief produced the migration code, the test could not contradict
/// it. The fixture has to come from the writer, not from the author.
/// </remarks>
public class CampaignMigrationTests
{
    private const string FirstLabel = "Yshtola Rhul";
    private const string SecondLabel = "Thancred Waters";

    /// <summary>Builds a real v1 store the way the previous build wrote one.</summary>
    private static string AV1StoreFromThePreviousBuild(out Guid first, out Guid second)
    {
        first = Guid.NewGuid();
        second = Guid.NewGuid();

        var document = new CampaignDocument();
        document.Campaigns.Add(new Campaign
        {
            CampaignId = first,
            PreferredCode = "BKD7RM",
            CreatedUtc = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero),
            Participants = { new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = FirstLabel } },
        });
        document.Campaigns.Add(new Campaign
        {
            CampaignId = second,
            PreferredCode = "XW2P4N",
            CreatedUtc = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            Participants = { new CampaignParticipant { ParticipantId = Guid.NewGuid(), Label = SecondLabel } },
        });

        return CampaignDocumentCodec.Serialize(document);
    }

    [Fact]
    public void TheFixtureIsAGenuineV1StoreAndNotSomethingThisTestInvented()
    {
        // Guards the premise of every other test in this file: if the previous build's writer ever
        // stops producing something the previous build's reader accepts, these fixtures are fiction
        // and the migration is being tested against nothing.
        var v1 = AV1StoreFromThePreviousBuild(out var first, out _);

        Assert.True(CampaignDocumentCodec.TryDeserialize(v1, out var reread));
        Assert.Equal(2, reread!.Campaigns.Count);
        Assert.Equal(CampaignDocument.CurrentSchemaVersion, reread.Version);
        Assert.Contains(reread.Campaigns, campaign => campaign.CampaignId == first);
    }

    [Fact]
    public void EveryCampaignInAV1StoreBecomesItsOwnFile()
    {
        var archive = new FakeCampaignArchive(AV1StoreFromThePreviousBuild(out var first, out var second));

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(2, store.Migrated);
        Assert.Equal(2, store.Campaigns.Count);
        Assert.Contains(CampaignFileName.NameFor(first), archive.Files.Keys);
        Assert.Contains(CampaignFileName.NameFor(second), archive.Files.Keys);
    }

    [Fact]
    public void EveryMigratedCampaignIsListableAndDeletableAfterwards()
    {
        var archive = new FakeCampaignArchive(AV1StoreFromThePreviousBuild(out var first, out var second));
        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.True(store.Delete(first));
        Assert.True(store.Delete(second));

        Assert.Empty(store.Campaigns);
        Assert.Empty(archive.Files);
    }

    [Fact]
    public void ParticipantsAndCodesSurviveTheMove()
    {
        var archive = new FakeCampaignArchive(AV1StoreFromThePreviousBuild(out var first, out var second));

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        var one = store.Find(first);
        var two = store.Find(second);
        Assert.NotNull(one);
        Assert.NotNull(two);
        Assert.Equal("BKD7RM", one!.PreferredCode);
        Assert.Equal(FirstLabel, Assert.Single(one.Participants).Label);
        Assert.Equal("XW2P4N", two!.PreferredCode);
        Assert.Equal(SecondLabel, Assert.Single(two.Participants).Label);
    }

    // Requirement 3: the old file must not survive as a second source of truth. If it did, the next
    // load would migrate it again over whatever the DM had since changed.
    [Fact]
    public void TheOldFileIsGoneAfterASuccessfulMigration()
    {
        var archive = new FakeCampaignArchive(AV1StoreFromThePreviousBuild(out _, out _));

        _ = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.DoesNotContain(CampaignFileName.LegacyFileName, archive.Files.Keys);
        Assert.Null(archive.ReadLegacy());
    }

    [Fact]
    public void MigrationDoesNotRunASecondTimeAndDoesNotUndoLaterEdits()
    {
        var archive = new FakeCampaignArchive(AV1StoreFromThePreviousBuild(out var first, out _));
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        store.Delete(first);

        var reloaded = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(0, reloaded.Migrated);
        Assert.Null(reloaded.Find(first));
        Assert.Single(reloaded.Campaigns);
    }

    // The old file is deleted only after every campaign has been written, so an interrupted
    // migration leaves it intact and is retried rather than losing the campaigns that had not been
    // written yet. Fails if the delete is moved before or into the write loop.
    [Fact]
    public void AnInterruptedMigrationLeavesTheOldFileIntactToBeRetried()
    {
        var v1 = AV1StoreFromThePreviousBuild(out _, out var second);
        var archive = new FakeCampaignArchive(v1) { FailWriteForName = CampaignFileName.NameFor(second) };

        Assert.Throws<IOException>(() => new CampaignStore(archive, new RecordingCampaignLog()));

        Assert.Equal(v1, archive.ReadLegacy());

        // And the retry succeeds once the failure clears, with nothing lost.
        archive.FailWriteForName = null;
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        Assert.Equal(2, store.Migrated);
        Assert.Equal(2, store.Campaigns.Count);
    }

    // A v1 store that will not parse cannot be migrated, and must not be discarded either: the
    // persisted-data rule's overwrite latitude is not available to this store. It is left exactly
    // where it is and listed so the DM can remove it (A-1.10).
    [Fact]
    public void AV1StoreThatWillNotParseIsLeftAloneAndListed()
    {
        const string Unreadable = "{ this was the old store and it is broken";
        var archive = new FakeCampaignArchive(Unreadable);
        var log = new RecordingCampaignLog();

        var store = new CampaignStore(archive, log);

        Assert.Equal(0, store.Migrated);
        Assert.Equal(Unreadable, archive.ReadLegacy());
        var entry = Assert.Single(store.Unreadable);
        Assert.Equal(CampaignFileName.LegacyFileName, entry.FileName);
        Assert.Equal(CampaignFileProblem.LeftByAnEarlierBuild, entry.Problem);
        Assert.Single(log.Warnings);

        // And it is deletable, which is the half that makes listing it worth anything.
        Assert.True(store.DeleteUnreadable(CampaignFileName.LegacyFileName));
        Assert.Null(archive.ReadLegacy());
    }

    [Fact]
    public void NoLogLineWrittenDuringMigrationCarriesAParticipantLabel()
    {
        var log = new RecordingCampaignLog();
        var archive = new FakeCampaignArchive(AV1StoreFromThePreviousBuild(out _, out _));

        _ = new CampaignStore(archive, log);

        Assert.NotEmpty(log.AllLines);
        Assert.All(log.AllLines, line =>
        {
            Assert.DoesNotContain(FirstLabel, line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SecondLabel, line, StringComparison.OrdinalIgnoreCase);
        });
    }
}
