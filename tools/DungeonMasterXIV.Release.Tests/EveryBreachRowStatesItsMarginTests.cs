using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-110: the parameter and nesting rows state a margin, like the length rows already do.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE BOUNDARY IS THE ENTIRE VALUE OF THE CHANGE</b>, so each row is asserted at three points
/// rather than at one clearly-over value. A test that only checks a breach proves the number is
/// printed; it does not show the reader can tell <i>at capacity and compliant</i> from <i>over</i>,
/// which is the confusion the margin exists to remove. The semantics are settled and not re-litigated
/// here: margin = block − value, <b>margin 0 is at capacity and compliant</b>, a negative margin is
/// the breach.
/// </para>
/// <para>
/// <b>Why this drives the tool as a process instead of calling a function.</b> The rows are formatted
/// inline in <c>Program.cs</c> and there is no seam to call. Extracting one would be a refactor this
/// fix was explicitly scoped out of, so the test reads <i>what a reader actually sees</i> — the only
/// artefact the change is about. One invocation covers every fixture; the cost is one process start.
/// </para>
/// <para>
/// <b>THE NESTING ROW HAS NO margin 1 CASE, AND THAT IS A PROPERTY OF THE LIMITS RATHER THAN A GAP
/// HERE.</b> Its flag is 3 and its block is 4, so the value one below the block is <i>at</i> the flag
/// and the row does not print at all. Its three points are therefore: depth 3 says NOTHING, depth 4 is
/// margin 0, depth 5 is margin −1. The silent case is asserted rather than skipped, because "prints
/// nothing" is the claim being made about it.
/// </para>
/// </remarks>
public class EveryBreachRowStatesItsMarginTests
{
    private static readonly Lazy<string> Report = new(Run);

    // PARAMETERS, all three points. Flag 4, block 6 -- so a 5-parameter member is the genuine
    // one-below-the-block case and margin 1 is reachable here.
    [Theory]
    [InlineData("FiveParameters", "5 parameters over the flag, margin 1")]
    [InlineData("SixParameters", "6 parameters over the flag, margin 0")]
    [InlineData("SevenParameters", "7 parameters OVER THE BLOCK, margin -1")]
    public void TheParameterRowStatesItsMargin(string member, string expected)
    {
        Assert.Contains(
            expected,
            LineFor(member),
            StringComparison.Ordinal);
    }

    // NESTING, at the block and over it. See the remarks for why there is no margin 1 case.
    [Theory]
    [InlineData("NestingFour", "nesting 4 over the flag, margin 0")]
    [InlineData("NestingFive", "nesting 5 OVER THE BLOCK, margin -1")]
    public void TheNestingRowStatesItsMargin(string member, string expected)
    {
        Assert.Contains(
            expected,
            LineFor(member),
            StringComparison.Ordinal);
    }

    // The third point for nesting, and it is an assertion rather than an omission: at depth 3 the row
    // is AT its flag, not over it, so it must say nothing at all. Fails if the flag comparison is
    // loosened to >= while nobody is looking at the boundary.
    [Fact]
    public void ANestingDepthAtTheFlagSaysNothing()
    {
        Assert.DoesNotContain("NestingThree", Report.Value, StringComparison.Ordinal);
    }

    // THE CONTROL. If the tool printed nothing at all -- a bad path, a build failure, an empty
    // fixture -- every Contains above would fail, but DoesNotContain above would PASS for the wrong
    // reason. This is what says the report is a real report.
    [Fact]
    public void TheToolProducedAReportAtAll()
    {
        Assert.Contains("Type span:", Report.Value, StringComparison.Ordinal);
        Assert.Contains("SixParameters", Report.Value, StringComparison.Ordinal);
    }

    private static string LineFor(string member) =>
        Report.Value
            .Split('\n')
            .SingleOrDefault(line => line.Contains(member, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"The report has no row for '{member}', so the margin assertion is about nothing:\n{Report.Value}");

    /// <summary>Runs the tool over one generated fixture and returns everything it printed.</summary>
    private static string Run()
    {
        var directory = Directory.CreateTempSubdirectory("bug110");
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
    /// One member per point under test, named so a row can be found without depending on line numbers.
    /// </summary>
    private const string Fixture = """
        class Fixture
        {
            public void FiveParameters(int a, int b, int c, int d, int e) { }

            public void SixParameters(int a, int b, int c, int d, int e, int f) { }

            public void SevenParameters(int a, int b, int c, int d, int e, int f, int g) { }

            public void NestingThree(int n)
            {
                if (n > 0)
                {
                    while (n > 1)
                    {
                        for (var i = 0; i < n; i++)
                        {
                            n--;
                        }
                    }
                }
            }

            public void NestingFour(int n)
            {
                if (n > 0)
                {
                    while (n > 1)
                    {
                        for (var i = 0; i < n; i++)
                        {
                            if (i > 2)
                            {
                                n--;
                            }
                        }
                    }
                }
            }

            public void NestingFive(int n)
            {
                if (n > 0)
                {
                    while (n > 1)
                    {
                        for (var i = 0; i < n; i++)
                        {
                            if (i > 2)
                            {
                                while (n > 3)
                                {
                                    n--;
                                }
                            }
                        }
                    }
                }
            }
        }
        """;
}
