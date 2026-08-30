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

    // ---- BUG-166: what an INTERRUPTED write leaves behind.

    /// <summary>
    /// <b>The case the successful-write test proves the file EXISTS for, and never checks.</b>
    /// <see cref="RetainedLogFileArchive.Write"/> writes <c>{id}.log.txt.writing</c> and then moves
    /// it into place; a crash between the two strands a COMPLETE session log. Both enumerations
    /// globbed <c>*.log.txt</c>, which requires the name to END with the extension — so the pending
    /// file matched neither, nothing could list it, and no delete path could remove it, while the
    /// shipped copy said <i>"nothing to delete anywhere but here"</i>.
    /// </summary>
    /// <remarks>
    /// <b><c>AWriteLeavesNoTemporaryFileBehind</c> above covers the SUCCESSFUL write</b> — so the
    /// pending file was known to exist, and the interrupted case is the only case it exists for.
    /// That is the one nobody wrote.
    /// </remarks>
    [Fact]
    public void AnInterruptedWriteLeavesAFileTheDeleteControlCanStillReach()
    {
        Directory.CreateDirectory(_directory);
        var pending = Guid.NewGuid() + ".log.txt.writing";
        File.WriteAllText(Path.Combine(_directory, pending), "a complete session log");

        var archive = Archive;

        // Listed...
        Assert.Contains(pending, archive.Unnameable());

        // ...and removable. Listing without deleting would leave the sentence just as false.
        Assert.True(archive.DeleteUnnameable(pending));
        Assert.False(File.Exists(Path.Combine(_directory, pending)));
    }

    /// <summary>
    /// The invariant rather than the instance: <b>NO file this archive can create may lie outside
    /// what the control can remove.</b>
    /// </summary>
    /// <remarks>
    /// <b>Written generically on purpose.</b> Pinning only the <c>.writing</c> suffix would fix the
    /// instance the reviewer found and leave the next shape to be discovered the same way — a glob
    /// is a guess about what this type writes, and the guess is what was wrong.
    /// </remarks>
    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111.log.txt.writing")]
    [InlineData("11111111-1111-1111-1111-111111111111.log.txt.tmp")]
    [InlineData("not-a-guid.log.txt")]
    [InlineData("stray")]
    public void AnythingInTheDirectoryThatIsNotAWellFormedLogIsSurfaced(string name)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, name), "x");

        Assert.Contains(name, Archive.Unnameable());
    }

    // THE BYSTANDER FOR THE WIDENED ENUMERATION: a well-formed log must STILL not be surfaced, or
    // "nothing is invisible" is satisfied by calling everything unnameable -- which would put real
    // campaign logs in a list the user is invited to delete as junk.
    [Fact]
    public void AWellFormedLogIsStillNotCalledUnnameable()
    {
        var archive = Archive;
        var campaign = Guid.NewGuid();
        archive.Write(campaign, "real");

        Assert.Empty(archive.Unnameable());
        Assert.Contains(campaign, archive.Campaigns());
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
