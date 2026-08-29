using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-114: the file and type margins are the numbers they claim to be, not merely labelled ones.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap fell between two fixes and belonged to neither.</b> BUG-111 made these two lines NAME
/// the row their margin belongs to, and its tests assert the LABEL. BUG-110 made the member rows
/// STATE a margin, and its tests assert the ARITHMETIC. The file and type rows <i>already printed a
/// margin</i> before either bug — so no test was ever written for a number that had always been
/// there, while the member rows got arithmetic tests because their margins were new. <b>A number
/// that predates both fixes inherits the coverage of neither.</b>
/// </para>
/// <para>
/// <b>Measured before this file existed:</b> changing <c>margin {margin}</c> to
/// <c>margin {margin + 1}</c> at <c>Program.cs:109</c>, and the equivalent at <c>:78</c>, each left
/// 1099 / 107 / 253 green. The tool printed a margin one larger than the truth and nothing anywhere
/// noticed.
/// </para>
/// <para>
/// <b>Why an ABSOLUTE assertion and not a relational one.</b> The tempting shape is two fixtures of
/// different sizes, asserting the margins differ by the difference in their line counts — no
/// constants, no drift. <b>It cannot catch this defect.</b> A constant off-by-one shifts both
/// margins equally and leaves the difference intact, so that test passes against the mutation it
/// exists to catch. The anchor has to be absolute.
/// </para>
/// <para>
/// <b>The block sizes are mirrored here deliberately, and a change to either SHOULD redden this.</b>
/// They are the limits four tickets were sequenced against; moving one is a decision, not a
/// refactor, and this test makes it cost one deliberate edit with the arithmetic written out beside
/// it.
/// </para>
/// </remarks>
public class TheMarginArithmeticIsTheArithmeticTests
{
    /// <summary>Mirrors <c>ClassBlock</c>, tools/DungeonMasterXIV.Sizes/Program.cs:13.</summary>
    private const int ClassBlock = 400;

    /// <summary>Mirrors <c>FileBlock</c>, tools/DungeonMasterXIV.Sizes/Program.cs:15.</summary>
    private const int FileBlock = 450;

    /// <summary>The fixture is six lines, and <see cref="ThePremiseHolds"/> asserts that rather than trusting it.</summary>
    private const int FixtureLines = 6;

    private static readonly Lazy<string> Report = new(Run);

    // THE FILE ROW. Fails on `FileBlock - lines.Length + 1`, which the whole suite missed.
    [Fact]
    public void TheFileMarginIsTheBlockMinusTheFilesLines()
    {
        Assert.Contains(
            $"margin {FileBlock - FixtureLines} lines",
            FileLine(),
            StringComparison.Ordinal);
    }

    // THE TYPE ROW, the one reproduced in the assignment. Fails on `margin + 1`.
    [Fact]
    public void TheTypeMarginIsTheBlockMinusTheTypesLines()
    {
        Assert.Contains(
            $"margin {ClassBlock - FixtureLines} lines",
            TypeLine(),
            StringComparison.Ordinal);
    }

    // THE PREMISE, ASSERTED RATHER THAN TRUSTED. Both tests above are arithmetic over a line count
    // this file merely believes. If the fixture is edited, or the tool's idea of a span changes, they
    // would go on comparing a margin against a number that is no longer the input -- passing or
    // failing for a reason that has nothing to do with the arithmetic. This is what makes them
    // legible when they break.
    [Fact]
    public void ThePremiseHolds()
    {
        Assert.Contains($"{FixtureLines} lines", FileLine(), StringComparison.Ordinal);
        Assert.Contains($"{FixtureLines} lines", TypeLine(), StringComparison.Ordinal);
    }

    // THE CONTROL. Every assertion here is Contains over a line located by substring; if the tool
    // produced nothing -- bad path, build failure, empty fixture -- the locator throws rather than
    // the assertion passing, but a report that ran and said nothing useful would still be silent.
    // qa-3's first control in a scratch tree came back 1 FAILED before they had mutated anything,
    // which is the reason this exists and the reason I ran mine before starting.
    [Fact]
    public void TheToolProducedAReportAtAll()
    {
        Assert.Contains("Type span:", Report.Value, StringComparison.Ordinal);
        Assert.Contains("Compliant", Report.Value, StringComparison.Ordinal);
    }

    private static string FileLine() =>
        Report.Value.Split('\n').SingleOrDefault(line => line.Contains("Fixture.cs", StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"No file line in the report:\n{Report.Value}");

    private static string TypeLine() =>
        Report.Value.Split('\n').SingleOrDefault(line =>
            line.Contains("Compliant", StringComparison.Ordinal) && line.Contains("margin", StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"No type-span line in the report:\n{Report.Value}");

    private static string Run()
    {
        var directory = Directory.CreateTempSubdirectory("bug114");
        var fixture = Path.Combine(directory.FullName, "Fixture.cs");
        File.WriteAllText(fixture, Fixture);

        using var tool = Process.Start(new ProcessStartInfo(
            "dotnet",
            $"run --project \"{Path.Combine(TheBuild.RepositoryRoot().FullName, "tools", "DungeonMasterXIV.Sizes")}\" -- \"{fixture}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Could not start the sizes tool.");

        var output = tool.StandardOutput.ReadToEnd() + tool.StandardError.ReadToEnd();
        tool.WaitForExit();

        directory.Delete(recursive: true);
        return output;
    }

    /// <summary>Six lines, compliant on every row, so both margins are ordinary subtractions.</summary>
    private const string Fixture = """
        class Compliant
        {
            public Compliant(int a, int b, int c)
            {
            }
        }
        """;
}
