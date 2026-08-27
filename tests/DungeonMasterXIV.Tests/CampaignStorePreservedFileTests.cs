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

    // Fails if the store hands an unvalidated caller-supplied name straight to the file layer.
    [Fact]
    public void ANameThisPluginDidNotWriteIsRefusedAndLogged()
    {
        var archive = new FakeCampaignArchive("{ not json");
        var log = new RecordingCampaignLog();
        var store = new CampaignStore(archive, log);

        Assert.False(store.DeletePreserved("../../dalamudUI.ini"));
        Assert.Single(store.PreservedFiles());
        Assert.Contains(log.Warnings, line => line.Contains("Refused to delete"));
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
