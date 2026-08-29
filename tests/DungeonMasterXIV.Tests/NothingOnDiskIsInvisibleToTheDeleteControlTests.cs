using System;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Data;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The three format defects the code reviewer found on #213, each pinned: a version in the header,
/// a write that cannot lose the whole history, and <b>nothing on disk that the delete control
/// cannot see</b>.
/// </summary>
/// <remarks>
/// <b>THE THIRD IS THE SHIPPED-COPY CLAUSE AGAIN, ONE LAYER DOWN.</b> <c>ConfigWindow</c> says
/// <i>"nothing to delete anywhere but here"</i>. The first version of the archive listed only files
/// it could parse as a campaign id and <b>silently skipped the rest</b> — so a file it could not name
/// was a file the control could not list, and one it could not list was one it could not delete.
/// That is the same failure the whole ticket exists to prevent, arriving through the enumeration
/// rather than through the absence of a button.
/// </remarks>
public class NothingOnDiskIsInvisibleToTheDeleteControlTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "dmx-logs-" + Guid.NewGuid().ToString("N"));

    private RetainedLogFileArchive Archive => new(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    // ---- the version.

    [Fact]
    public void EveryExportCarriesTheFormatVersion()
    {
        var log = new RetainedLog(Guid.NewGuid(), 1, [new LoggedEntry(new LoggedStamp(1, 1), "message", "BCDFGH", "hi")]);

        var exported = LogExport.Write(log);

        Assert.Contains($"version: {LogExport.FormatVersion}", exported, StringComparison.Ordinal);
    }

    // ---- the write.

    [Fact]
    public void AWriteLeavesNoTemporaryFileBehind()
    {
        Archive.Write(Guid.NewGuid(), "contents");

        // A .writing file surviving would mean the move never happened, and the next reader would
        // see a directory with a half-written twin of every log in it.
        Assert.Empty(Directory.GetFiles(_directory, "*.writing"));
    }

    [Fact]
    public void RewritingALogReplacesItRatherThanAppending()
    {
        var campaign = Guid.NewGuid();
        var archive = Archive;

        archive.Write(campaign, "first");
        archive.Write(campaign, "second");

        Assert.Equal("second", archive.Read(campaign));
        Assert.Single(Directory.GetFiles(_directory, "*.log.txt"));
    }

    // ---- nothing invisible.

    [Fact]
    public void AFileTheArchiveCannotNameIsSurfacedRatherThanSkipped()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "not-a-guid.log.txt"), "orphan");

        var unnameable = Archive.Unnameable();

        Assert.Equal(["not-a-guid.log.txt"], unnameable);
    }

    // THE BYSTANDER: a properly named log must NOT appear in the unnameable list, or "surface
    // everything" could be implemented by surfacing everything twice.
    [Fact]
    public void APropertlyNamedLogIsNotListedAsUnnameable()
    {
        var archive = Archive;
        archive.Write(Guid.NewGuid(), "fine");
        File.WriteAllText(Path.Combine(_directory, "rubbish.log.txt"), "orphan");

        Assert.Single(archive.Unnameable());
        Assert.Single(archive.Campaigns());
    }

    [Fact]
    public void AnUnnameableFileCanBeDeleted()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "not-a-guid.log.txt");
        File.WriteAllText(path, "orphan");

        Assert.True(Archive.DeleteUnnameable("not-a-guid.log.txt"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DeletingAnUnnameableFileCannotReachOutsideTheLogDirectory()
    {
        Directory.CreateDirectory(_directory);
        var outside = Path.Combine(Path.GetTempPath(), "dmx-outside-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(outside, "must survive");

        try
        {
            // A traversal attempt must not delete it. The name is reduced to its file part first.
            Archive.DeleteUnnameable(Path.Combine("..", "..", Path.GetFileName(outside)));

            Assert.True(File.Exists(outside), "A traversal reached outside the log directory.");
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void AnArchiveWithNoDirectoryYetAnswersEmptyRatherThanThrowing()
    {
        // A DM who has never hosted has no logs. Asking must answer "none".
        Assert.Empty(Archive.Campaigns());
        Assert.Empty(Archive.Unnameable());
        Assert.Null(Archive.Read(Guid.NewGuid()));
    }
}
