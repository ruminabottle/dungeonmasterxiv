using System;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The file adapter, tested against a real directory. This is what taking a <c>DirectoryInfo</c>
/// instead of Dalamud's plugin interface bought: the guard that stands between a caller and an
/// arbitrary file delete is now exercised in the layer that holds the path, rather than asserted
/// one layer up where it could never fire.
/// </summary>
public sealed class CampaignFileArchiveTests : IDisposable
{
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
    public void NothingStoredReadsAsNullRatherThanEmpty()
    {
        Assert.Null(NewArchive().Read());
    }

    [Fact]
    public void WhatIsWrittenIsWhatIsReadBack()
    {
        var archive = NewArchive();

        archive.Write("{\"Version\":1}");

        Assert.Equal("{\"Version\":1}", archive.Read());
    }

    [Fact]
    public void PreservingKeepsTheOldFileAndLeavesTheDocumentSlotEmpty()
    {
        var archive = NewArchive();
        archive.Write("unreadable");

        var keptAs = archive.PreserveUnreadable();

        Assert.Null(archive.Read());
        Assert.Equal("unreadable", File.ReadAllText(PathTo(keptAs)));
        Assert.Equal(keptAs, Assert.Single(archive.PreservedFiles()));
    }

    [Fact]
    public void DeletingAPreservedFileRemovesItFromDisk()
    {
        var archive = NewArchive();
        archive.Write("unreadable");
        var keptAs = archive.PreserveUnreadable();

        Assert.True(archive.DeletePreserved(keptAs));

        Assert.False(File.Exists(PathTo(keptAs)));
        Assert.Empty(archive.PreservedFiles());
    }

    // THE test this move existed to make possible. Every name here is a concrete input that,
    // without the guard, deletes something it must not — proven by removing the guard and watching
    // each case fail, not by inspection.
    //
    // One case that is NOT here, and why: "campaigns.unreadable-../../outside.txt" looks like the
    // nastiest of the set and is inert. Path normalisation treats "campaigns.unreadable-.." as a
    // directory name, so the ".." that follows only cancels it and the path lands back INSIDE this
    // directory on a file that does not exist. It returns false with or without the guard, which
    // makes it a case that cannot fail dressed as the most dangerous one. Replaced with a name that
    // genuinely escapes: a valid preserved name followed by a separator and two levels up.
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("campaigns.unreadable-x.json/../../outside.txt")]
    [InlineData("campaigns.json")]
    public void ANameThatIsNotAPreservedFileDeletesNothing(string name)
    {
        var archive = NewArchive();
        var bystander = new FileInfo(Path.Combine(_directory.FullName, "..", "outside.txt"));
        File.WriteAllText(bystander.FullName, "not ours to delete");
        archive.Write("{\"Version\":1}");

        try
        {
            Assert.False(archive.DeletePreserved(name));

            Assert.True(File.Exists(bystander.FullName), "a file outside the directory was deleted");
            Assert.NotNull(archive.Read());
        }
        finally
        {
            bystander.Delete();
        }
    }

    [Fact]
    public void OnlyFilesThisPluginPreservedAreListed()
    {
        var archive = NewArchive();
        File.WriteAllText(PathTo("dalamudUI.ini"), "someone else's file");
        File.WriteAllText(PathTo("campaigns.json"), "the live document");
        archive.Write("unreadable");
        var keptAs = archive.PreserveUnreadable();

        Assert.Equal(new[] { keptAs }, archive.PreservedFiles().ToArray());
    }
}
