using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// Every arm of the gate, fired. The ones this repository cannot supply are fired synthetically.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS TREE CAN ONLY EXERCISE ONE OF THE FIVE ROWS.</b> All seven of its block breaches are
/// method <i>length</i>; there is no class breach, no file breach, no parameter breach and no nesting
/// breach anywhere in it. So a gate that silently covered only length would be green on <c>main</c>,
/// green on every branch, and green in any test written against real code — which is why the rows
/// below are driven by constructed sources rather than by the repository.
/// </para>
/// <para>
/// <b>And each fixture is proved to measure something before its red is trusted.</b> A fixture the
/// readers cannot parse and a gate that ignores the row produce the same green. The
/// <c>...IsMeasuredAtAll</c> tests are that control: they assert the fixture yields a measurement,
/// independently of whether the gate refuses it.
/// </para>
/// </remarks>
public class TheSizeGateRefusesWhatItShouldTests
{
    private const string Path = "Fixture.cs";

    private static IReadOnlyList<string> RefusalsFor(string source, IReadOnlyList<Breach>? baseline = null)
    {
        var measured = SizeGate.BreachesIn(Path, source);
        return SizeGate.Refusals(baseline ?? [], measured.Breaches, [], [Path]);
    }

    // ---------- the two rows this repository CAN exercise ----------

    [Fact]
    public void ACompliantSourceIsNotRefused()
    {
        var refusals = RefusalsFor("namespace F;\npublic sealed class Small\n{\n    public int One() => 1;\n}\n");

        Assert.Empty(refusals);
    }

    [Fact]
    public void ANewMethodLengthBreachIsRefused()
    {
        var body = string.Join('\n', Enumerable.Repeat("        var x = 1;", 70));
        var source = $"namespace F;\npublic sealed class Big\n{{\n    public void Long()\n    {{\n{body}\n    }}\n}}\n";

        var refusals = RefusalsFor(source);

        Assert.Contains(refusals, r => r.Contains("NEW METHOD BREACH", System.StringComparison.Ordinal));
    }

    // ---------- the rows this repository CANNOT exercise ----------

    [Fact]
    public void AParameterBreachIsMeasuredAtAll()
    {
        var measured = SizeGate.BreachesIn(Path, SevenParameters);

        Assert.Empty(measured.Unmeasured);
        Assert.Contains(measured.Breaches, b => b.Row == SizeGate.ParameterRow);
    }

    [Fact]
    public void AParameterBreachIsRefused()
    {
        var refusals = RefusalsFor(SevenParameters);

        Assert.Contains(refusals, r => r.Contains("NEW PARAMETERS BREACH", System.StringComparison.Ordinal));
    }

    [Fact]
    public void ANestingBreachIsMeasuredAtAll()
    {
        var measured = SizeGate.BreachesIn(Path, FiveDeep);

        Assert.Empty(measured.Unmeasured);
        Assert.Contains(measured.Breaches, b => b.Row == SizeGate.NestingRow);
    }

    [Fact]
    public void ANestingBreachIsRefused()
    {
        var refusals = RefusalsFor(FiveDeep);

        Assert.Contains(refusals, r => r.Contains("NEW NESTING BREACH", System.StringComparison.Ordinal));
    }

    [Fact]
    public void AClassBreachIsRefusedABSOLUTELY_EvenWhenTheBaselineRecordsIt()
    {
        var filler = string.Join('\n', Enumerable.Repeat("    // line", 405));
        var source = $"namespace F;\npublic sealed class Huge\n{{\n{filler}\n}}\n";
        var pretendGrandfathered = new[] { new Breach(Path, SizeGate.ClassRow, "Huge", 409, 400) };

        var refusals = RefusalsFor(source, pretendGrandfathered);

        // The point of the absolute rows: grandfathering must NOT rescue them.
        Assert.Contains(refusals, r => r.Contains("CLASS BLOCK", System.StringComparison.Ordinal));
    }

    [Fact]
    public void AFileBreachIsRefusedABSOLUTELY()
    {
        var source = string.Join('\n', Enumerable.Repeat("// line", 460));

        var refusals = RefusalsFor(source);

        Assert.Contains(refusals, r => r.Contains("FILE BLOCK", System.StringComparison.Ordinal));
    }

    // ---------- the delta arms, which must fire for their OWN reason ----------

    [Fact]
    public void AGrandfatheredBreachAtItsRecordedMarginIsNotRefused()
    {
        var body = string.Join('\n', Enumerable.Repeat("        var x = 1;", 70));
        var source = $"namespace F;\npublic sealed class Big\n{{\n    public void Long()\n    {{\n{body}\n    }}\n}}\n";
        var measured = SizeGate.BreachesIn(Path, source);
        var recorded = measured.Breaches.Where(b => b.Row == SizeGate.MethodRow).ToList();

        var refusals = SizeGate.Refusals(recorded, measured.Breaches, [], [Path]);

        Assert.Empty(refusals);
    }

    [Fact]
    public void AWorsenedBreachIsRefusedForWORSENING_NotAsANewOne()
    {
        var body = string.Join('\n', Enumerable.Repeat("        var x = 1;", 70));
        var source = $"namespace F;\npublic sealed class Big\n{{\n    public void Long()\n    {{\n{body}\n    }}\n}}\n";
        var now = SizeGate.BreachesIn(Path, source).Breaches.Single(b => b.Row == SizeGate.MethodRow);
        // Recorded as it was BEFORE it grew: same unit, better margin.
        var wasBetter = new[] { now with { Value = now.Value - 5 } };

        var refusals = SizeGate.Refusals(wasBetter, [now], [], [Path]);

        var single = Assert.Single(refusals);
        Assert.StartsWith("WORSENED:", single, System.StringComparison.Ordinal);
        Assert.DoesNotContain("NEW ", single, System.StringComparison.Ordinal);
    }

    // ---------- intake, the arm a delta gate cannot see from its own results ----------

    [Fact]
    public void AFileLeavingIntakeIsRefused()
    {
        var refusals = SizeGate.Refusals([], [], ["src/Gone.cs", "src/Stays.cs"], ["src/Stays.cs"]);

        var single = Assert.Single(refusals);
        Assert.StartsWith("INTAKE:", single, System.StringComparison.Ordinal);
        Assert.Contains("src/Gone.cs", single, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AFileARRIVINGInIntakeIsNotRefused()
    {
        // The floor is one-directional on purpose: a new file needs no baseline edit, a departing
        // one does. Otherwise every PR that adds a file would have to touch the baseline.
        var refusals = SizeGate.Refusals([], [], ["src/Stays.cs"], ["src/Stays.cs", "src/New.cs"]);

        Assert.Empty(refusals);
    }

    private const string SevenParameters = """
        namespace F;
        public sealed class Wide
        {
            public void Seven(int a, int b, int c, int d, int e, int f, int g)
            {
                _ = a + b + c + d + e + f + g;
            }
        }
        """;

    private const string FiveDeep = """
        namespace F;
        public sealed class Deep
        {
            public void Nested(int n)
            {
                if (n > 0)
                {
                    while (n > 1)
                    {
                        for (var i = 0; i < n; i++)
                        {
                            if (i % 2 == 0)
                            {
                                foreach (var c in "x")
                                {
                                    _ = c;
                                }
                            }
                        }
                    }
                }
            }
        }
        """;
}
