using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Preserving an unreadable document is only half of A-1.10. The other half is being able to get
/// rid of what was preserved, because those files keep participant labels indefinitely.
/// </summary>
public class CampaignStorePreservedFileTests
{
    [Fact]
    public void APreservedFileIsListedSoTheDmCanSeeItExists()
    {
        var archive = new FakeCampaignArchive("{ not json");

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(CampaignLoadOutcome.Unreadable, store.LoadOutcome);
        Assert.Single(store.PreservedFiles());
    }

    [Fact]
    public void DeletingAPreservedFileRemovesItFromTheListing()
    {
        var archive = new FakeCampaignArchive("{ not json");
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        var name = Assert.Single(store.PreservedFiles());

        Assert.True(store.DeletePreserved(name));
        Assert.Empty(store.PreservedFiles());
    }

    // The name guard itself is the archive's and is tested there, against a real directory, in
    // CampaignFileArchiveTests. What belongs here is the store's half: a refusal is reported rather
    // than swallowed, and nothing is dropped from the listing on the way.
    [Fact]
    public void ARefusedDeletionIsReportedAndChangesNothing()
    {
        var archive = new FakeCampaignArchive("{ not json");
        var log = new RecordingCampaignLog();
        var store = new CampaignStore(archive, log);
        var revisionBefore = store.Revision;

        Assert.False(store.DeletePreserved("../../dalamudUI.ini"));

        Assert.Single(store.PreservedFiles());
        Assert.Equal(revisionBefore, store.Revision);
        Assert.Contains(log.Warnings, line => line.Contains("Could not delete"));
    }

    // The window rebuilds its cached rows and its preserved-file list from this, so a deletion the
    // DM performs has to be visible on the next frame.
    [Fact]
    public void DeletingAPreservedFileMovesTheRevisionSoTheDrawPathRebuilds()
    {
        var archive = new FakeCampaignArchive("{ not json");
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        var before = store.Revision;

        store.DeletePreserved(Assert.Single(store.PreservedFiles()));

        Assert.NotEqual(before, store.Revision);
    }

    [Fact]
    public void NoPreservedFileIsListedWhenNothingWasEverUnreadable()
    {
        var store = new CampaignStore(new FakeCampaignArchive(), new RecordingCampaignLog());

        Assert.Empty(store.PreservedFiles());
    }
}
