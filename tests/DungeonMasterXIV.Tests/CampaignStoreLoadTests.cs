using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The load path: telling first run from a document that will not read, and — the rule that binds
/// from this chunk onward — keeping the one that will not read.
/// </summary>
public class CampaignStoreLoadTests
{
    [Fact]
    public void NothingStoredIsAFirstRunAndWritesNoFile()
    {
        var archive = new FakeCampaignArchive();
        var log = new RecordingCampaignLog();

        var store = new CampaignStore(archive, log);

        Assert.Equal(CampaignLoadOutcome.FirstRun, store.LoadOutcome);
        Assert.Null(store.LoadedVersion);
        Assert.Empty(store.Campaigns);
        Assert.Empty(archive.Writes);
        Assert.Single(log.Informations);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void AStoredDocumentIsLoadedWithTheVersionItArrivedUnder()
    {
        var archive = new FakeCampaignArchive(
            "{\"Version\":1,\"Campaigns\":[{\"CampaignId\":\"2f1d5b8e-0000-4000-8000-000000000001\"}]}");

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(CampaignLoadOutcome.Loaded, store.LoadOutcome);
        Assert.Equal(1, store.LoadedVersion);
        Assert.Single(store.Campaigns);
    }

    [Fact]
    public void AnUnreadableDocumentIsKeptAndNotWrittenOver()
    {
        // The standards permit overwriting unreadable state only while it is trivially
        // reconstructible. A campaign roster is not, and this chunk is the trigger that ends the
        // permission. Fails if the store writes defaults over the file, which is what the config
        // store is allowed to do and this one is not.
        const string Unreadable = "{ this is not json";
        var archive = new FakeCampaignArchive(Unreadable);
        var log = new RecordingCampaignLog();

        var store = new CampaignStore(archive, log);

        Assert.Equal(CampaignLoadOutcome.Unreadable, store.LoadOutcome);
        Assert.Equal(Unreadable, archive.Preserved);
        Assert.Empty(archive.Writes);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ADocumentFromANewerBuildIsUnreadableRatherThanMisread()
    {
        // "Unreadable" is not a synonym for "corrupt": this document is well-formed and simply
        // not ours to interpret. Fails if the codec ignores the version and reads it anyway.
        var fromTheFuture =
            $"{{\"Version\":{CampaignDocument.CurrentSchemaVersion + 1},\"Campaigns\":[]}}";
        var archive = new FakeCampaignArchive(fromTheFuture);

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(CampaignLoadOutcome.Unreadable, store.LoadOutcome);
        Assert.Equal(fromTheFuture, archive.Preserved);
    }

    [Fact]
    public void FirstRunAndFailedToLoadAreDistinguishableInTheLogAsWellAsInTheOutcome()
    {
        var firstRun = new RecordingCampaignLog();
        var failed = new RecordingCampaignLog();

        _ = new CampaignStore(new FakeCampaignArchive(), firstRun);
        _ = new CampaignStore(new FakeCampaignArchive("not json"), failed);

        Assert.Empty(firstRun.Warnings);
        Assert.Single(failed.Warnings);
        Assert.NotEqual(firstRun.Informations.Count, failed.Informations.Count);
    }
}
