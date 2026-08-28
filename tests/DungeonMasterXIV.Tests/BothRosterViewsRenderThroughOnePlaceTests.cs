using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The DM's roster and a player's roster are drawn by the same code (R-1.3f).
/// </summary>
/// <remarks>
/// <para>
/// <b>The two sides arrive as different types from different places and must still look the
/// same.</b> The host reads its own <c>Audience</c> because it AUTHORS the roster (D-3); a player
/// reads what the host sent. Nothing stops someone rendering each where it is read, and then the
/// unknown-role rule exists in two places and drifts — which is exactly the shape that has bitten
/// this codebase before, where a rule held on one side and quietly did not on the other.
/// </para>
/// <para>
/// <b>Source-reading, because no test can execute this file.</b> The test project references Core
/// alone and may never reference the plugin, so <c>SessionWindow</c> cannot be constructed here.
/// Reading the source is the established way round that — <c>TlsBypassFenceTests</c> is the
/// precedent — and its limit is stated rather than implied: this asserts the SHAPE of the code, not
/// that the pixels are right. A-1.13 proper is in-game and this chunk cannot discharge it.
/// </para>
/// </remarks>
public class BothRosterViewsRenderThroughOnePlaceTests
{
    [Fact]
    public void ThereIsExactlyOneRosterRenderer()
    {
        Assert.Single(Regex.Matches(Code(), @"void\s+DrawRoster\s*\("));
    }

    [Fact]
    public void BothSidesCallIt()
    {
        // Definition plus at least two call sites. Fewer means one of the two views renders its own
        // way, or does not render at all.
        Assert.True(
            Regex.Matches(Code(), @"DrawRoster\s*\(").Count >= 3,
            "One or both roster views stopped going through DrawRoster.");
    }

    // The rule this centralisation exists to protect. If role labelling appears anywhere but the one
    // renderer, the unknown-role decision has a second home and can differ between the two views.
    [Fact]
    public void RoleLabellingHappensOnlyInTheRenderer()
    {
        Assert.Single(Regex.Matches(Code(), @"SessionRoleLabel\."));
    }

    // The DM renders from what it AUTHORS and the player from what it RECEIVED. Swapping either is
    // a D-3 violation that would still compile and still draw names.
    [Fact]
    public void TheHostRendersItsOwnAudienceAndThePlayerRendersWhatItWasSent()
    {
        var code = Code();

        Assert.Contains("audience.Recipients.Select", code, StringComparison.Ordinal);
        Assert.Contains("_coordinator.Roster.Select", code, StringComparison.Ordinal);
    }

    // THE VACUITY CONTROL. Every assertion above is a match count against a string; if the reader
    // returned an empty string, the "exactly one" tests would fail loudly but nothing proves the
    // file being read is the right one. This names something only SessionWindow contains.
    [Fact]
    public void TheReaderIsReadingSessionWindow()
    {
        var code = Code();

        Assert.NotEmpty(code);
        Assert.Contains("private void DrawHosting()", code, StringComparison.Ordinal);
        Assert.Contains("private void DrawJoining()", code, StringComparison.Ordinal);
    }

    /// <summary>The window's source with comment lines removed.</summary>
    /// <remarks>
    /// Stripped because this file's own commentary names <c>DrawRoster</c> and
    /// <c>SessionRoleLabel</c> while explaining them, and a count that includes prose counts the
    /// explanation as a second implementation.
    /// </remarks>
    private static string Code()
    {
        var source = Path.Combine(RepositoryRoot(), "Windows", "SessionWindow.cs");

        Assert.True(File.Exists(source), $"No SessionWindow.cs at '{source}'.");

        return string.Join(
            "\n",
            File.ReadAllLines(source).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "SessionWindow.cs")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No repository root above {AppContext.BaseDirectory}.");
    }
}
