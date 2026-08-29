using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The gate itself, applied to the tree this test is running in.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY MEASURING THE WORKING TREE IS MEASURING THE MERGE, AND DO NOT "FIX" THIS INTO A BRANCH-TIP
/// CHECK.</b> The rule is that size is judged on <c>merge(main, branch)</c>, never on the branch tip
/// — and this test computes no merge. It does not need to: the Deployment Manager refuses to merge
/// any PR that does not already contain <c>origin/main</c>, so <b>every mergeable branch already
/// contains main and the working tree IS the merged tree by construction.</b> The guarantee lives at
/// that chokepoint, not in this check. Remove the chokepoint and this test quietly becomes the
/// branch-tip check the rule forbids, which is why the reasoning is written here rather than assumed.
/// </para>
/// <para>
/// <b>THREE WAYS THIS GATE COULD BE GREEN BY NOT LOOKING, and each is guarded separately.</b>
/// </para>
/// <list type="number">
/// <item>Covering only one size row — this tree's seven breaches are ALL method length, so a gate
/// blind to the other four rows is green everywhere. Guarded by the synthetic fixtures in
/// <c>TheSizeGateRefusesWhatItShouldTests</c>, not here; no real input can fire those rows.</item>
/// <item>Measuring fewer files than exist — guarded by the baseline floor below.</item>
/// <item>Counting its own output and finding it consistent. <b>Self-consistent arithmetic proves
/// nothing</b>: BUG-121's aborted host prints <c>Failed 0 / Passed 299 / Total 299</c>, which
/// reconciles perfectly and is false. The check below is NOT that — its right-hand side is the intake
/// from <c>git ls-files</c>, established independently of the loop it is checking.</item>
/// </list>
/// </remarks>
public class TheSizeGateHoldsThisTreeTests(ITestOutputHelper output)
{
    [ContainsMainFact]
    public void TheGateRefusesNothingOnThisTree()
    {
        var intake = SizeGateIntake.Files();
        var expected = SizeGateBaseline.Files();

        // BOTH OPERANDS ASSERTED NON-EMPTY BEFORE THEY ARE COMPARED, because two empty sets AGREE.
        // `expected.Except(intake)` is empty when the floor is empty, when the intake is empty, and
        // when the tree is genuinely clean -- three states, one answer. The baseline already refuses
        // a zero-entry floor and the intake already throws when git cannot answer, so neither can be
        // empty by the time this runs; asserting it here says so at the point of comparison rather
        // than leaving it to two guarantees made elsewhere.
        Assert.True(expected.Count > 0, "the population floor is empty, so the comparison below would "
            + "pass against any tree at all");
        Assert.True(intake.Count > 0, "the intake is empty, so nothing was measured and the "
            + "comparison below would pass by agreeing with an empty floor");

        // (2) THE POPULATION FLOOR, AND IT IS THE ONLY ARM THAT CATCHES A SHORT RUN. A file that
        // leaves intake has no baseline and therefore no delta, so a regression in it reads as zero.
        var departed = expected.Except(intake, System.StringComparer.Ordinal).ToList();
        Assert.True(
            departed.Count == 0,
            $"{departed.Count} file(s) left intake and are no longer measured: "
            + $"{string.Join(", ", departed)}. Restore them or record the removal in the baseline.");

        var breaches = new List<Breach>();
        var unmeasured = new List<string>();
        var read = new List<string>();
        foreach (var path in intake)
        {
            var measured = SizeGate.BreachesIn(path, SizeGateIntake.Read(path));
            breaches.AddRange(measured.Breaches);
            unmeasured.AddRange(measured.Unmeasured);
            read.Add(path);
        }

        // (3) LOOP COMPLETENESS AS A SET, NOT A COUNT. The right-hand side comes from git, outside
        // the loop being checked, so this is not the self-consistency BUG-121 defeats.
        //
        // AND IT COMPARES THE FILE NAMES RATHER THAN HOW MANY THERE WERE. A count reduces a vector
        // to a scalar and then watches one element of it: reading one file twice while skipping
        // another leaves the count identical and the coverage wrong. Naming which element a guard
        // watches is the discipline; here the answer had to be "all of them".
        var missed = intake.Except(read, System.StringComparer.Ordinal).ToList();
        var stray = read.Except(intake, System.StringComparer.Ordinal).ToList();
        Assert.True(
            missed.Count == 0 && stray.Count == 0 && read.Count == read.Distinct().Count(),
            $"coverage is not the intake: {missed.Count} never read ({string.Join(", ", missed.Take(5))}), "
            + $"{stray.Count} read but not in intake, "
            + $"{read.Count - read.Distinct().Count()} read more than once.");

        // A refusal is not a pass. A span the reader could not measure has not been found compliant.
        Assert.True(
            unmeasured.Count == 0,
            $"{unmeasured.Count} span(s) were REFUSED, so they were not measured and cannot be "
            + $"reported compliant:\n  {string.Join("\n  ", unmeasured)}");

        output.WriteLine($"coverage: {read.Count}/{intake.Count} files read, {unmeasured.Count} spans refused, "
            + $"{breaches.Count} breaches found, floor {expected.Count} files");

        var refusals = SizeGate.Refusals(SizeGateBaseline.Breaches(), breaches, expected, intake);

        Assert.True(refusals.Count == 0, "The size gate refuses this tree:\n  " + string.Join("\n  ", refusals));
    }

    // THE DUPLICATED LIMITS ARE POLICED. They cannot be referenced from Program.cs -- top-level
    // consts in an entry-point file -- and the ticket's boundary is to use the sizes tool AS-IS
    // rather than restructure it. So they are copied, and this fails the moment the copies disagree.
    [Theory]
    [InlineData("ClassBlock", SizeGate.ClassBlock)]
    [InlineData("FileBlock", SizeGate.FileBlock)]
    [InlineData("MethodBlock", SizeGate.MethodBlock)]
    [InlineData("ParameterBlock", SizeGate.ParameterBlock)]
    [InlineData("NestingBlock", SizeGate.NestingBlock)]
    public void TheGateHoldsTheSameLimitTheToolDoes(string name, int mine)
    {
        var program = SizeGateIntake.Read("tools/DungeonMasterXIV.Sizes/Program.cs");
        var match = System.Text.RegularExpressions.Regex.Match(
            program, $@"const int {name} = (\d+);");

        Assert.True(match.Success, $"Program.cs no longer declares `const int {name}` — the gate "
            + "cannot confirm it holds the tool's limit, which is worse than holding a stale one.");
        Assert.Equal(int.Parse(match.Groups[1].Value), mine);
    }
}
