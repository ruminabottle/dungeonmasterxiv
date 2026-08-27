using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The load path: telling first run from a folder whose files will not read, and leaving what will
/// not read exactly where it is.
/// </summary>
public class CampaignStoreLoadTests
{
    private static string ACampaignFileFor(Campaign campaign) => CampaignFileCodec.Serialize(campaign);

    [Fact]
    public void AnEmptyFolderIsAFirstRunAndWritesNothing()
    {
        var archive = new FakeCampaignArchive();
        var log = new RecordingCampaignLog();

        var store = new CampaignStore(archive, log);

        Assert.Equal(CampaignLoadOutcome.FirstRun, store.LoadOutcome);
        Assert.Empty(store.Campaigns);
        Assert.Empty(store.Unreadable);
        Assert.Empty(archive.Writes);
        Assert.Empty(archive.Deletes);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void EachCampaignFileBecomesOneCampaign()
    {
        var archive = new FakeCampaignArchive();
        var first = new Campaign { CampaignId = System.Guid.NewGuid(), PreferredCode = "BKD7RM" };
        var second = new Campaign { CampaignId = System.Guid.NewGuid(), PreferredCode = "XW2P4N" };
        archive.Place(CampaignFileName.NameFor(first.CampaignId), ACampaignFileFor(first));
        archive.Place(CampaignFileName.NameFor(second.CampaignId), ACampaignFileFor(second));

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(CampaignLoadOutcome.Loaded, store.LoadOutcome);
        Assert.Equal(2, store.Campaigns.Count);
        Assert.Empty(store.Unreadable);
    }

    // A campaign file that will not parse is LEFT ALONE and LISTED. Both halves matter: the
    // persisted-data rule forbids overwriting it because a campaign roster is not trivially
    // reconstructible, and A-1.10 requires the DM can still see and remove it.
    [Fact]
    public void ACampaignFileThatWillNotParseIsListedAndNotOverwritten()
    {
        const string Unreadable = "{ this is not json";
        var archive = new FakeCampaignArchive();
        var name = CampaignFileName.NameFor(System.Guid.NewGuid());
        archive.Place(name, Unreadable);

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Empty(store.Campaigns);
        var entry = Assert.Single(store.Unreadable);
        Assert.Equal(name, entry.FileName);
        Assert.Equal(CampaignFileProblem.WillNotParse, entry.Problem);
        Assert.Equal(Unreadable, archive.Files[name]);
        Assert.Empty(archive.Writes);
    }

    // "Unreadable" is not a synonym for "corrupt": this file is well-formed and simply not ours to
    // interpret. Fails if the codec ignores the version and reads it anyway.
    [Fact]
    public void ACampaignFileFromANewerBuildIsListedRatherThanMisread()
    {
        var archive = new FakeCampaignArchive();
        var name = CampaignFileName.NameFor(System.Guid.NewGuid());
        archive.Place(name, $"{{\"Version\":{CampaignFileDocument.CurrentSchemaVersion + 1},\"Campaign\":{{}}}}");

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Empty(store.Campaigns);
        Assert.Equal(CampaignFileProblem.WillNotParse, Assert.Single(store.Unreadable).Problem);
    }

    // First run and failed load must stay distinguishable — the standards say so, and a restructure
    // is exactly where that gets lost.
    [Fact]
    public void AFolderOfUnreadableFilesIsNotReportedAsAFirstRun()
    {
        var firstRun = new CampaignStore(new FakeCampaignArchive(), new RecordingCampaignLog());

        var broken = new FakeCampaignArchive();
        broken.Place(CampaignFileName.NameFor(System.Guid.NewGuid()), "not json");
        var failed = new CampaignStore(broken, new RecordingCampaignLog());

        Assert.Equal(CampaignLoadOutcome.FirstRun, firstRun.LoadOutcome);
        Assert.Equal(CampaignLoadOutcome.Unreadable, failed.LoadOutcome);
        Assert.NotEqual(firstRun.LoadOutcome, failed.LoadOutcome);
    }

    [Fact]
    public void OneUnreadableFileDoesNotHideTheCampaignsThatDoRead()
    {
        var archive = new FakeCampaignArchive();
        var good = new Campaign { CampaignId = System.Guid.NewGuid(), PreferredCode = "BKD7RM" };
        archive.Place(CampaignFileName.NameFor(good.CampaignId), ACampaignFileFor(good));
        archive.Place(CampaignFileName.NameFor(System.Guid.NewGuid()), "not json");

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(CampaignLoadOutcome.Loaded, store.LoadOutcome);
        Assert.Equal(good.CampaignId, Assert.Single(store.Campaigns).CampaignId);
        Assert.Single(store.Unreadable);
    }

    [Fact]
    public void ACampaignSurvivesBeingWrittenAndLoadedAgain()
    {
        var archive = new FakeCampaignArchive();
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        var created = store.Create(SessionCode.FromValid("BKD7RM"));
        store.AddParticipant(created.CampaignId, "Yshtola Rhul");

        var reloaded = new CampaignStore(archive, new RecordingCampaignLog());

        var campaign = Assert.Single(reloaded.Campaigns);
        Assert.Equal(created.CampaignId, campaign.CampaignId);
        Assert.Equal("BKD7RM", campaign.PreferredCode);
        Assert.Equal("Yshtola Rhul", Assert.Single(campaign.Participants).Label);
    }
}
