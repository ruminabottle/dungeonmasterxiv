using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-111: the type-span line says which row its margin belongs to, so a silent parameter row
/// cannot be answered by the number next to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>AN ABSENT ROW IS NOT READ AS ABSENT.</b> A type under the parameter flag prints no parameter
/// row at all — correctly, because the tool prints a member line only when it has something to say.
/// But a reader who arrived asking about parameters does not see a gap; they see the numbers that
/// ARE there and fill the gap with them. Confirmed, not hypothetical: <c>InboundHandlers</c> printed
/// <c>4 lines (113-116) under the flag, margin 396</c> and was read as "4 members, margin 396" by
/// somebody reasoning about where parameters could be added. The true figures were 3 against a flag
/// of 4 and a block of 6.
/// </para>
/// <para>
/// <b>EVERY TEST HERE ASSERTS THE COMPLIANT CASE, and that is the opposite of BUG-110's
/// requirement.</b> The breaching case already prints, and is already guarded — a test over it would
/// pass against the fixed tool and the broken one alike, because the defect is entirely in what a
/// COMPLIANT type's line lets a reader conclude. The natural instinct after BUG-110 is to reach for
/// boundaries again; boundaries are exactly what cannot see this.
/// </para>
/// <para>
/// <b>Both halves are asserted: what the printed line SAYS, and that the absent row stays absent.</b>
/// Fixing the ambiguity by printing a compliant parameter row on every member would contradict the
/// tool's own ruling that one line per member, only when it has something to say, is what keeps the
/// class and file lines findable. So the silence is preserved and the adjacent line is made
/// unmistakable instead.
/// </para>
/// </remarks>
public class ACompliantTypesLineCannotBeReadAsParameterRoomTests
{
    private static readonly Lazy<string> Report = new(Run);

    // THE FIX. The standing and the margin each name the row they belong to, so neither can be
    // borrowed by a reader asking about a different row. Fails if either reverts to a bare "flag" or
    // an unlabelled margin.
    [Fact]
    public void TheTypeSpanLineNamesTheRowItsMarginBelongsTo()
    {
        var line = LineFor("Compliant");

        Assert.Contains("under the class flag", line, StringComparison.Ordinal);
        Assert.Contains("lines", line[line.IndexOf("margin", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    // The file line had the same defect and is asserted for the same reason -- it sits directly above
    // the type line, so two unlabelled margins appeared adjacently and either could be borrowed.
    [Fact]
    public void TheFileLineNamesTheRowItsMarginBelongsTo()
    {
        var line = Report.Value.Split('\n').First(l => l.Contains("Fixture.cs", StringComparison.Ordinal));

        Assert.Contains("under the file flag", line, StringComparison.Ordinal);
        Assert.Contains("lines", line[line.IndexOf("margin", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    // THE SILENCE IS PRESERVED, and this is the half a "just print everything" fix would break. The
    // fixture's constructor takes three parameters against a flag of four, so it must print NO
    // parameter row -- the tool's ruling is one line per member and only when it has something to
    // say. If this fails, the ambiguity was closed by adding noise rather than by adding a label.
    [Fact]
    public void ACompliantMemberStillPrintsNoParameterRow()
    {
        Assert.DoesNotContain("parameters", Report.Value, StringComparison.Ordinal);
    }

    // THE CONTROL. Two assertions above are DoesNotContain or depend on a line existing; if the tool
    // produced nothing at all -- bad path, build failure, empty fixture -- the silence assertion
    // would pass for entirely the wrong reason. This is what says the report is a real report.
    [Fact]
    public void TheToolProducedAReportAtAll()
    {
        Assert.Contains("Type span:", Report.Value, StringComparison.Ordinal);
        Assert.Contains("Compliant", Report.Value, StringComparison.Ordinal);
    }

    private static string LineFor(string name) =>
        Report.Value
            .Split('\n')
            .SingleOrDefault(line => line.Contains(name, StringComparison.Ordinal) && line.Contains("lines", StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"The report has no type-span line for '{name}':\n{Report.Value}");

    private static string Run()
    {
        var directory = Directory.CreateTempSubdirectory("bug111");
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

    /// <summary>
    /// The shape that was misread: compliant on every row, with a constructor whose parameter count
    /// is under the flag and therefore silent.
    /// </summary>
    private const string Fixture = """
        class Compliant
        {
            public Compliant(int a, int b, int c)
            {
            }
        }
        """;
}
