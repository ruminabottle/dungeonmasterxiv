using System;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.10 as extended on 2026-08-27: the DM lists and deletes <b>every</b> campaign the machine
/// holds, including files the plugin cannot read or parse.
/// </summary>
/// <remarks>
/// Constructing an unreadable file is not the test. Asserting it is <i>listed and deletable</i> is
/// — a fixture that builds the alarming case and then checks only that the readable ones still
/// appear would pass while the criterion failed.
/// </remarks>
public class CampaignStoreUnreadableFileTests
{
    private static FakeCampaignArchive WithAnUnreadableCampaignFile(out string name)
    {
        var archive = new FakeCampaignArchive();
        name = CampaignFileName.NameFor(Guid.NewGuid());
        archive.Place(name, "{ this is not json");
        return archive;
    }

    [Fact]
    public void AnUnreadableFileIsListedRatherThanIgnored()
    {
        var archive = WithAnUnreadableCampaignFile(out var name);

        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.Equal(name, Assert.Single(store.Unreadable).FileName);
    }

    [Fact]
    public void AnUnreadableFileCanBeDeletedAndLeavesDisk()
    {
        var archive = WithAnUnreadableCampaignFile(out var name);
        var store = new CampaignStore(archive, new RecordingCampaignLog());

        Assert.True(store.DeleteUnreadable(name));

        Assert.Empty(store.Unreadable);
        Assert.DoesNotContain(name, archive.Files.Keys);
    }

    [Fact]
    public void AFileLeftByAnEarlierBuildIsListedAndDeletable()
    {
        var archive = new FakeCampaignArchive();
        var name = PreservedCampaignFile.NameFor(DateTimeOffset.UtcNow);
        archive.Place(name, "kept by an older version, may hold labels");
        var store = new CampaignStore(archive, new RecordingCampaignLog());

        var entry = Assert.Single(store.Unreadable);
        Assert.Equal(CampaignFileProblem.LeftByAnEarlierBuild, entry.Problem);
        Assert.True(store.DeleteUnreadable(name));
        Assert.DoesNotContain(name, archive.Files.Keys);
    }

    // Fails if the store forwards an arbitrary caller-supplied name to the file layer. The store's
    // half is that it only deletes something it is actually holding; the path guard itself is the
    // archive's and is tested against a real directory in CampaignFileArchiveTests.
    [Fact]
    public void ANameTheStoreIsNotHoldingIsRefusedAndLogged()
    {
        var archive = WithAnUnreadableCampaignFile(out _);
        var log = new RecordingCampaignLog();
        var store = new CampaignStore(archive, log);

        Assert.False(store.DeleteUnreadable("../../dalamudUI.ini"));

        Assert.Single(store.Unreadable);
        Assert.Empty(archive.Deletes);
        Assert.Contains(log.Warnings, line => line.Contains("Refused to delete"));
    }

    [Fact]
    public void DeletingAnUnreadableFileMovesTheRevisionSoTheDrawPathRebuilds()
    {
        var archive = WithAnUnreadableCampaignFile(out var name);
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        var before = store.Revision;

        store.DeleteUnreadable(name);

        Assert.NotEqual(before, store.Revision);
    }

    [Fact]
    public void NothingIsListedAsUnreadableWhenEveryFileReads()
    {
        var archive = new FakeCampaignArchive();
        var store = new CampaignStore(archive, new RecordingCampaignLog());
        store.Create(null);

        Assert.Empty(new CampaignStore(archive, new RecordingCampaignLog()).Unreadable);
    }
}
