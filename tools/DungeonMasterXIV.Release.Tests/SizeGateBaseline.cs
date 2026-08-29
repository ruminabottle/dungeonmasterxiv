using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The expected population and the grandfathered breaches, held outside the run.
/// </summary>
/// <remarks>
/// <b>A GATE THAT CHECKS ITS OWN ARITHMETIC IS CHECKING THAT IT ADDED UP WHAT IT SAW.</b> It has no
/// way to know what it did not see. The only thing separating <i>measured everything and found
/// nothing</i> from <i>stopped early and found nothing</i> is an expected count established
/// independently of the run — which is this file. BUG-121 is the same defect one layer up:
/// <c>dotnet test</c> prints <c>Failed 0 / Passed 299 / Total 299</c> on an aborted host, which is
/// internally consistent and completely false.
/// </remarks>
internal static class SizeGateBaseline
{
    private const string FileName = "size-gate-baseline.txt";

    /// <summary>Every file the gate is expected to measure. A FLOOR — arrivals are fine, departures are not.</summary>
    /// <remarks>
    /// <b>AN UNPARSEABLE BASELINE REFUSES RATHER THAN COMPARING, and this arm is not symmetric with
    /// <see cref="Breaches"/>.</b> A floor that parses to the empty set makes the departure check
    /// vacuous — `expected.Except(intake)` is empty for every possible tree, so the gate reports a
    /// clean population having compared nothing. That fails OPEN. An empty BREACH set fails CLOSED
    /// instead: every existing breach would read as NEW and the gate would refuse loudly. So only
    /// this one needs the guard, and saying which is the point — a check that fires on nothing must
    /// not sit where a working one should be.
    /// </remarks>
    public static IReadOnlyList<string> Files()
    {
        var files = Lines("FILE ").Order(StringComparer.Ordinal).ToList();

        return files.Count > 0
            ? files
            : throw new InvalidOperationException(
                $"{FileName} yielded ZERO 'FILE ' lines. The gate has no population floor, so its "
                + "departure check would pass against any tree at all. Refusing rather than "
                + "comparing against nothing.");
    }

    /// <summary>The grandfathered breaches, at the margins they are allowed to keep.</summary>
    public static IReadOnlyList<Breach> Breaches() =>
        [.. Lines("BREACH ").Select(Parse)];

    private static Breach Parse(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 5)
        {
            throw new InvalidOperationException(
                $"Malformed BREACH line in {FileName}: '{line}'. Expected file|row|unit|value|capacity.");
        }

        return new Breach(parts[0], parts[1], parts[2], int.Parse(parts[3]), int.Parse(parts[4]));
    }

    private static IEnumerable<string> Lines(string prefix)
    {
        var path = Path.Combine(SourceDirectory(), FileName);
        var all = File.ReadAllLines(path);

        // NOT SILENTLY EMPTY. A baseline that fails to load leaves the gate with no floor and no
        // grandfathered set -- it would then report every existing breach as NEW, or, if the intake
        // floor were the empty set, wave through a file that had left measurement.
        if (all.Length == 0)
        {
            throw new InvalidOperationException($"{path} is empty. The gate has no expected population.");
        }

        return all
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line[prefix.Length..].Trim());
    }

    /// <summary>Where this test project's sources live, so the baseline travels with them.</summary>
    private static string SourceDirectory()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, FileName)))
        {
            here = here.Parent;
        }

        return here?.FullName
            ?? Path.Combine(TheBuild.RepositoryRoot().FullName, "tools", "DungeonMasterXIV.Release.Tests");
    }
}
