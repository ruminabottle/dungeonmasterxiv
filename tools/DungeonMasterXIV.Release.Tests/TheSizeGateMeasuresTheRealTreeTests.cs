using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DungeonMasterXIV.Release.Tests;

public class TheSizeGateMeasuresTheRealTreeTests(ITestOutputHelper output)
{
    [Fact]
    public void TheIntakeIsNonEmptyAndComesFromGit()
    {
        var files = SizeGateIntake.Files();

        Assert.NotEmpty(files);
        Assert.Contains("Plugin.cs", files);
        output.WriteLine($"intake: {files.Count} files");
    }

    [Fact]
    public void WhatTheGateFindsOnThisTree()
    {
        var files = SizeGateIntake.Files();
        var breaches = new List<Breach>();
        var unmeasured = new List<string>();
        foreach (var path in files)
        {
            var measured = SizeGate.BreachesIn(path, SizeGateIntake.Read(path));
            breaches.AddRange(measured.Breaches);
            unmeasured.AddRange(measured.Unmeasured);
        }

        // COVERAGE IS PART OF THE RESULT. A refusal is not a pass -- a span the reader could not
        // measure has not been found compliant, and a gate that drops refusals reports "no breaches"
        // for a file it never read.
        output.WriteLine($"unmeasured spans: {unmeasured.Count}");
        foreach (var refusal in unmeasured)
        {
            output.WriteLine($"    {refusal}");
        }

        Assert.Empty(unmeasured);

        output.WriteLine($"files: {files.Count}");
        foreach (var row in breaches.GroupBy(b => b.Row))
        {
            output.WriteLine($"  {row.Key}: {row.Count()}");
        }

        foreach (var breach in breaches.OrderBy(b => b.Key))
        {
            output.WriteLine($"    {breach}");
        }

        // DELIBERATELY NOT Assert.NotEmpty(breaches), which is what this said first. That is a test
        // that reddens the day somebody fixes the last of the seven -- an assertion that the codebase
        // stays broken. What is worth pinning here is that the measurement covered the whole intake;
        // whether it found anything is the gate's business, not this diagnostic's.
        Assert.Equal(files.Count, files.Distinct().Count());
    }
}
