using System;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The file adapter, against a real directory. It takes a <c>DirectoryInfo</c> rather than
/// Dalamud's plugin interface precisely so the guard that stands between a caller and an arbitrary
/// file delete is exercised in the layer that holds the path.
/// </summary>
public sealed class CampaignFileArchiveTests : IDisposable
{
    private static readonly Guid AnId = new("2f1d5b8e-0000-4000-8000-000000000001");

    private readonly DirectoryInfo _directory;

    public CampaignFileArchiveTests()
    {
        _directory = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), "dmx-archive-" + Guid.NewGuid().ToString("N")));
        _directory.Create();
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private CampaignFileArchive NewArchive() => new(_directory);

    private string PathTo(string name) => Path.Combine(_directory.FullName, name);

    [Fact]
    public void AnEmptyFolderHoldsNoCampaignsAndNoLegacyFile()
    {
        var archive = NewArchive();

        Assert.Empty(archive.CampaignFiles());
        Assert.Null(archive.ReadLegacy());
        Assert.Empty(archive.OtherOwnedFiles());
    }

    [Fact]
    public void ACampaignIsWrittenToItsOwnFileAndReadBack()
    {
        var archive = NewArchive();
        var name = CampaignFileName.NameFor(AnId);

        archive.WriteCampaign(name, "{\"Version\":1}");

        Assert.Equal(new[] { name }, archive.CampaignFiles().ToArray());
        Assert.Equal("{\"Version\":1}", archive.ReadCampaign(name));
    }

    [Fact]
    public void TheLegacyFileIsReadableAndIsNotCountedAsACampaignFile()
    {
        File.WriteAllText(PathTo(CampaignFileName.LegacyFileName), "old store");
        var archive = NewArchive();

        Assert.Equal("old store", archive.ReadLegacy());
        Assert.Empty(archive.CampaignFiles());
        Assert.Contains(CampaignFileName.LegacyFileName, archive.OtherOwnedFiles());
    }

    [Fact]
    public void FilesBelongingToOtherPluginsAreNeitherListedNorDeletable()
    {
        File.WriteAllText(PathTo("dalamudUI.ini"), "someone else's file");
        var archive = NewArchive();

        Assert.Empty(archive.CampaignFiles());
        Assert.Empty(archive.OtherOwnedFiles());
        Assert.False(archive.Delete("dalamudUI.ini"));
        Assert.True(File.Exists(PathTo("dalamudUI.ini")));
    }

    [Fact]
    public void DeletingACampaignFileRemovesItFromDisk()
    {
        var archive = NewArchive();
        var name = CampaignFileName.NameFor(AnId);
        archive.WriteCampaign(name, "{\"Version\":1}");

        Assert.True(archive.Delete(name));

        Assert.False(File.Exists(PathTo(name)));
        Assert.Empty(archive.CampaignFiles());
    }

    // Every name here deletes something it must not if the guard is removed — verified by removing
    // it and watching each case fail, not by inspection. A file outside the directory is written
    // first and asserted to survive, so "returned false" cannot pass for "deleted nothing".
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("campaign-2f1d5b8e-0000-4000-8000-000000000001.json/../../outside.txt")]
    [InlineData("dalamudUI.ini")]
    public void ANameOutsideOurOwnFilesDeletesNothing(string name)
    {
        var archive = NewArchive();
        var bystander = new FileInfo(Path.Combine(_directory.FullName, "..", "outside.txt"));
        File.WriteAllText(bystander.FullName, "not ours to delete");
        // Written so the "dalamudUI.ini" case is a REAL target. Without this file present,
        // File.Exists rejects that name on its own and the case passes with or without the guard --
        // an inert case sitting in a list of dangerous ones. Found by removing the guard and
        // watching only two of the three fail.
        File.WriteAllText(PathTo("dalamudUI.ini"), "another plugin's settings");
        archive.WriteCampaign(CampaignFileName.NameFor(AnId), "{\"Version\":1}");

        try
        {
            Assert.False(archive.Delete(name));

            Assert.True(File.Exists(bystander.FullName), "a file outside the directory was deleted");
            Assert.True(File.Exists(PathTo("dalamudUI.ini")), "another plugin's file was deleted");
            Assert.Single(archive.CampaignFiles());
        }
        finally
        {
            bystander.Delete();
        }
    }

    [Fact]
    public void WritingUnderANameThatIsNotACampaignFileIsRefusedOutright()
    {
        var archive = NewArchive();

        Assert.Throws<ArgumentException>(() => archive.WriteCampaign("../outside.txt", "anything"));
        Assert.False(File.Exists(Path.Combine(_directory.FullName, "..", "outside.txt")));
    }

    [Fact]
    public void APreservedFileFromAnEarlierBuildIsListedAndDeletable()
    {
        var name = PreservedCampaignFile.NameFor(DateTimeOffset.UtcNow);
        File.WriteAllText(PathTo(name), "kept by an older version");
        var archive = NewArchive();

        Assert.Contains(name, archive.OtherOwnedFiles());
        Assert.True(archive.Delete(name));
        Assert.False(File.Exists(PathTo(name)));
    }
}
