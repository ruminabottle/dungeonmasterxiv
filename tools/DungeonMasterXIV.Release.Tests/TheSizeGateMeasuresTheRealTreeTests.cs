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
        foreach (var path in files)
        {
            breaches.AddRange(SizeGate.BreachesIn(path, SizeGateIntake.Read(path)));
        }

        output.WriteLine($"files: {files.Count}");
        foreach (var row in breaches.GroupBy(b => b.Row))
        {
            output.WriteLine($"  {row.Key}: {row.Count()}");
        }

        foreach (var breach in breaches.OrderBy(b => b.Key))
        {
            output.WriteLine($"    {breach}");
        }

        Assert.NotEmpty(breaches);
    }
}
