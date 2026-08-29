using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// DMXENG-107: a flag the tree did not cross before and crosses now is REPORTED, not refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE GAP THIS CLOSES.</b> The gate held five constants and all five were BLOCKS. The flag row
/// existed in the tool, which prints it, and in the standard, which rules it, and in the gate not at
/// all — so <c>InboundFrame</c> went 236 UNDER to 261 OVER across #212 and every gate report was
/// honest about what it measured and silent about what it did not.
/// </para>
/// <para>
/// <b>REPORTING, NEVER REFUSING.</b> <c>engineering-standards.md:1140</c> — <i>"Blocking limits are a
/// denial on their own. Flags are a conversation."</i> A gate that refused here would not be stricter;
/// it would implement a different rule. <see cref="TheFlagReportCannotMakeTheGateRefuse"/> is the
/// assertion, because a doc comment saying "non-blocking" is not a mechanism.
/// </para>
/// <para>
/// <b>Fixtures rather than the real tree, for the reason <c>SizeGate</c> already gives.</b> Every
/// block breach on <c>main</c> is a method-length breach, so a gate exercised only against real code
/// has arms no input can fire. The same is true one row over: constructing the crossing is the only
/// way to prove the crossing is seen.
/// </para>
/// </remarks>
public class TheGateReportsANewlyCrossedFlagTests
{
    private const string Path = "src/Fixture.cs";

    /// <summary>A class of <paramref name="span"/> body lines — over 250 flags, over 400 blocks.</summary>
    /// <summary>A class of <paramref name="span"/> body lines under a chosen name.</summary>
    private static string ClassNamed(string name, int span) =>
        $"namespace F;\npublic sealed class {name}\n{{\n{string.Join('\n', Enumerable.Repeat("    // x", span))}\n}}\n";

    private static string ClassOf(int span) =>
        $"namespace F;\npublic sealed class Wide\n{{\n{string.Join('\n', Enumerable.Repeat("    // x", span))}\n}}\n";

    private static IReadOnlyList<Breach> FlagsOf(string source) =>
        SizeGate.FlagCrossingsIn(Path, source).Breaches;

    // >>> OBLIGATION 1: A FLAG CROSSED WITHOUT A BLOCK CROSSED IS REPORTED <<<
    //
    // 260 lines is OVER the class flag of 250 and UNDER the class block of 400. A fixture that
    // crossed the block would prove nothing -- the existing gate already catches those, so this test
    // would pass with the whole flag mechanism deleted.
    [Fact]
    public void AFlagCrossedWithoutABlockCrossedIsReported()
    {
        var before = FlagsOf(ClassOf(240));
        var after = FlagsOf(ClassOf(260));

        var crossed = SizeGateFlags.NewlyCrossedFlags(before, after);

        var crossing = Assert.Single(crossed);
        Assert.Equal(SizeGate.ClassRow, crossing.Row);
        Assert.Equal(SizeGate.ClassFlag, crossing.Capacity);

        // THE PREMISE, ASSERTED RATHER THAN ASSUMED: no BLOCK was crossed by either side. Without
        // this the test would still pass on a build where the flag path is quietly reading blocks.
        Assert.Empty(SizeGate.BreachesIn(Path, ClassOf(260)).Breaches);
    }

    // >>> OBLIGATION 2: THE CONTROL THAT STAYS SILENT <<<
    //
    // A number that MOVES without CROSSING must produce nothing. A report that speaks on every change
    // is noise, and noise nobody reads is the same failure one step later. Without this row, a build
    // that reports every measurement passes the test above.
    [Fact]
    public void ANumberThatMovesWithoutCrossingIsNotReported()
    {
        var crossed = SizeGateFlags.NewlyCrossedFlags(FlagsOf(ClassOf(100)), FlagsOf(ClassOf(240)));

        Assert.Empty(crossed);
        Assert.Empty(SizeGateFlags.FlagReport(FlagsOf(ClassOf(100)), FlagsOf(ClassOf(240))));
    }

    // The other direction of the same control: ALREADY over stays quiet. It is not NEWLY crossed, and
    // the deliberate exclusion is recorded on NewlyCrossedFlags rather than left to be inferred here.
    [Fact]
    public void AFlagThatWasAlreadyCrossedIsNotReportedAgain()
    {
        // 280 rather than 300, and the first draft's 300 is worth recording: ClassOf(300) is ~304
        // FILE lines, so it newly crossed the FILE flag as well and this assertion failed. The
        // mechanism was right and the FIXTURE was wrong -- a fixture built to move one row that
        // quietly moves a second is how a delta test comes to assert something other than its name.
        Assert.Empty(SizeGateFlags.NewlyCrossedFlags(FlagsOf(ClassOf(260)), FlagsOf(ClassOf(280))));
    }

    // The premise of the row above, pinned so it cannot rot into the same fixture error: neither
    // side may cross the FILE flag, or "already crossed" would be measuring the wrong row.
    [Fact]
    public void NeitherSideOfTheAlreadyCrossedFixtureTouchesTheFileFlag()
    {
        foreach (var crossings in new[] { FlagsOf(ClassOf(260)), FlagsOf(ClassOf(280)) })
        {
            Assert.DoesNotContain(crossings, crossing => crossing.Row == SizeGate.FileRow);
            Assert.Contains(crossings, crossing => crossing.Row == SizeGate.ClassRow);
        }
    }

    // >>> OBLIGATION 4: THE REPORT NAMES ITS ROW AND ITS DIRECTION (BUG-111) <<<
    //
    // A bare margin says nothing about WHICH limit it is a margin from, and an absent row is not read
    // as absent -- the reader fills the gap with whichever row they arrived asking about.
    [Fact]
    public void TheReportNamesWhichRowAndWhichDirection()
    {
        var crossed = SizeGateFlags.NewlyCrossedFlags(FlagsOf(ClassOf(240)), FlagsOf(ClassOf(260)));

        var report = SizeGateFlags.FlagReport(FlagsOf(ClassOf(240)), FlagsOf(ClassOf(260)));
        var line = report[^1];
        Assert.Contains("CLASS FLAG", line);
        Assert.Contains("250", line);
        Assert.Contains(Path, line);
        Assert.Contains("Wide", line);
    }

    // >>> THE RULED CONSTRAINT, AS A MECHANISM RATHER THAN A COMMENT <<<
    //
    // Shape 2 is OUT: a flag may never refuse. This is what makes that true of the BUILD rather than
    // of the prose -- Refusals is handed a tree whose flag is crossed and must still say nothing.
    [Fact]
    public void TheFlagReportCannotMakeTheGateRefuse()
    {
        var source = ClassOf(260);
        var current = SizeGate.BreachesIn(Path, source).Breaches;

        Assert.NotEmpty(SizeGate.FlagCrossingsIn(Path, source).Breaches);   // the flag IS crossed
        Assert.Empty(SizeGate.Refusals(current, current, [Path], [Path]));  // and the gate is silent
    }

    // >>> #216 REVIEW: THE REPORT STATES THE TOTALS IT WAS COMPUTED FROM <<<
    //
    // The totals are the disambiguator for this mechanism's own documented false positive, and the
    // first draft left them in <remarks> -- where no reader of the REPORT will ever look. A reader
    // had to have read the type's doc to survive the rename case; now the artefact tells them.
    [Fact]
    public void TheReportStatesTheTotalsItWasComputedFrom()
    {
        var report = SizeGateFlags.FlagReport(FlagsOf(ClassOf(240)), FlagsOf(ClassOf(260)));

        Assert.Contains("0 flag crossing(s) before", report[0]);
        Assert.Contains("1 after", report[0]);
        Assert.Contains("1 NEWLY crossed", report[0]);
    }

    // A RENAME: the same over-flag class under a new name. One key retires, another arrives, so the
    // TOTALS DO NOT MOVE while a new crossing appears -- exactly the case the type documents.
    [Fact]
    public void EqualTotalsBesideANewCrossingCarryTheRenameCaution()
    {
        var before = FlagsOf(ClassNamed("Wide", 260));
        var after = FlagsOf(ClassNamed("Broad", 260));

        var report = SizeGateFlags.FlagReport(before, after);

        Assert.Equal(before.Count, after.Count);
        Assert.Contains("rename signature", report[0]);
    }

    // >>> THE CONTROL, WITHOUT WHICH A BUILD THAT ALWAYS PRINTS THE CAUTION PASSES <<<
    //
    // A genuine new crossing RAISES the total, and must NOT be explained away as a possible rename.
    // A caution that fires on everything would turn the one signal that separates the two cases into
    // noise attached to both.
    [Fact]
    public void ARisingTotalDoesNotCarryTheRenameCaution()
    {
        var before = FlagsOf(ClassOf(240));
        var after = FlagsOf(ClassOf(260));

        var report = SizeGateFlags.FlagReport(before, after);

        Assert.NotEqual(before.Count, after.Count);
        Assert.DoesNotContain("rename signature", report[0]);
    }
}