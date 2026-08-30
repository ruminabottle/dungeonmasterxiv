using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// DMXENG-112: the report reaches a reader from a REAL TREE WALK, not a constructed pair.
/// </summary>
/// <remarks>
/// <b>THE SENTENCE #216's TESTS DID NOT SAY.</b> They proved the reporter works when given a pair;
/// none proved anyone gives it one. So the fixtures here are two REAL revisions of a REAL file read
/// out of git, and the walk is the thing under test rather than the reporting.
/// </remarks>
public class TheGateReportsFlagsFromARealTreeTests(ITestOutputHelper output)
{
    // A real file that really crossed the real file flag, found in this repository's own history:
    // 287 lines at 8a80c2f, 320 now. OVER the file flag of 300 and UNDER the block of 450 -- the
    // flag-without-block case, supplied by history rather than by a fixture I designed to pass.
    private const string Crossed = "src/DungeonMasterXIV.Relay/Sessions/SessionRegistry.cs";
    private const string Before = "8a80c2f";

    [Fact]
    public void AWalkOverRealRevisionsReportsAFileThatCrossedARealFlag()
    {
        var report = FlagSupply.ReportFor(
            [Crossed],
            path => FlagSupply.SourceAt(Before, path),
            path => SizeGateIntake.Read(path));

        Assert.NotEmpty(report);
        Assert.Contains(report, line => line.Contains(Crossed) && line.Contains("FILE FLAG"));
        output.WriteLine(string.Join('\n', report));
    }

    // THE PREMISE, asserted rather than assumed: the "before" side really is under the flag and the
    // "after" side really is over it. Without this the test above could pass on a build that reports
    // every file, or on a fixture whose two sides were never different.
    [Fact]
    public void TheTwoRevisionsAreGenuinelyOneUnderAndOneOverTheFileFlag()
    {
        var before = FlagSupply.SourceAt(Before, Crossed);
        Assert.NotNull(before);

        Assert.DoesNotContain(
            SizeGate.FlagCrossingsIn(Crossed, before!).Breaches,
            crossing => crossing.Row == SizeGate.FileRow);
        Assert.Contains(
            SizeGate.FlagCrossingsIn(Crossed, SizeGateIntake.Read(Crossed)).Breaches,
            crossing => crossing.Row == SizeGate.FileRow);

        // And no BLOCK is crossed on either side, or the existing gate would already catch it and
        // this would be proving something the flag row is not responsible for.
        Assert.Empty(SizeGate.BreachesIn(Crossed, SizeGateIntake.Read(Crossed)).Breaches);
    }

    // >>> THE CONTROL: A TREE THAT CROSSED NOTHING SAYS NOTHING <<<
    //
    // Same file, same revision on both sides. Without this row, a walk that reported every crossing
    // it found -- rather than every crossing that is NEW -- passes the test above.
    [Fact]
    public void AWalkOverATreeThatCrossedNothingIsSilent()
    {
        var report = FlagSupply.ReportFor(
            [Crossed],
            path => FlagSupply.SourceAt(Before, path),
            path => FlagSupply.SourceAt(Before, path)!);

        Assert.Empty(report);
    }

    // A file absent from the base ref has no prior crossings, so one that ARRIVES over a flag is
    // correctly newly crossed. Null from the reader is not an error and must not be read as one.
    [Fact]
    public void AFileAbsentFromTheBaseRefIsMeasuredWithNoPriorCrossings()
    {
        var report = FlagSupply.ReportFor(
            [Crossed],
            _ => null,
            path => SizeGateIntake.Read(path));

        Assert.Contains(report, line => line.Contains("0 flag crossing(s) before"));
    }

    // >>> THE LIVE CONSUMER. THIS IS THE SUPPLY THAT WAS MISSING <<<
    //
    // ContainsMainFact for the same reason the block gate carries it: a delta against a base ref we
    // cannot read is not a weaker answer, it is a different one wearing a pass. The skip names why.
    [ContainsMainFact]
    public void TheGateReportsWhatThisTreeNewlyCrossed()
    {
        var changed = FlagSupply.ChangedAgainst("origin/main");

        // NULL IS NOT EMPTY. Empty means nothing changed; null means git could not say, and
        // reporting silence on that arm is the fail-open this whole gate exists to prevent.
        Assert.True(changed is not null,
            "git could not list what this tree changed against origin/main, so the flag delta was "
            + "not computed. Reporting no crossings here would be a clean answer nobody measured.");

        var intake = SizeGateIntake.Files().ToHashSet(System.StringComparer.Ordinal);
        var walked = changed!.Where(intake.Contains).ToList();

        var report = FlagSupply.ReportFor(
            walked,
            path => FlagSupply.SourceAt("origin/main", path),
            SizeGateIntake.Read);

        output.WriteLine($"flag delta: {walked.Count} of {changed.Count} changed file(s) are in intake, "
            + $"measured against origin/main. {report.Count} line(s) to report.");
        foreach (var line in report)
        {
            output.WriteLine("  " + line);
        }
    }

    // >>> THE FAIL-OPEN ARM, DRIVEN <<<
    //
    // git diff CANNOT SEE UNTRACKED FILES -- measured: with two new files on disk,
    // `git diff --name-only origin/main` returned empty. So the walk unions in untracked additions,
    // or a brand-new file arriving over a flag is reported by nothing until somebody commits it.
    [Fact]
    public void UntrackedAdditionsAreWalkedToo()
    {
        var combined = FlagSupply.Combine(["a.cs"], ["b.cs"]);

        Assert.Equal(["a.cs", "b.cs"], combined);
    }

    // AND NULL PROPAGATES RATHER THAN SHORTENING THE LIST. "Could not ask" must never arrive looking
    // like "nothing to walk" -- a partial walk reported as a clean one is the fail-open this gate
    // exists to prevent. Both directions, because one arm guarded is one arm unguarded.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void IfEitherListCannotBeReadTheAnswerIsCouldNotAsk(bool trackedFailed, bool untrackedFailed)
    {
        Assert.Null(FlagSupply.Combine(
            trackedFailed ? null : ["a.cs"],
            untrackedFailed ? null : ["b.cs"]));
    }

    // The control for the row above: two readable lists DO produce an answer, so the assertions
    // there are about null-ness rather than about Combine never returning anything.
    [Fact]
    public void TwoReadableListsProduceAnAnswer()
    {
        Assert.NotNull(FlagSupply.Combine([], []));
    }
}