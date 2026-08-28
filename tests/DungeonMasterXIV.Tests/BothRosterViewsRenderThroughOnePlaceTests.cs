using System;
using System.Collections.Generic;
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
        var code = Code();

        Assert.Single(Regex.Matches(code, @"class\s+RosterView\b"));
        Assert.Single(Regex.Matches(code, @"static\s+void\s+Draw\s*\("));
    }

    [Fact]
    public void BothSidesCallIt()
    {
        // Two call sites, in two different files since DMXENG-15. Fewer means one of the two views
        // renders its own way, or does not render at all. The definition is no longer counted here
        // — it is a different string now (RosterView.Draw at the call, static void Draw at the
        // definition), which is why the threshold moved from 3 to 2 rather than the guard weakening.
        Assert.True(
            Regex.Matches(Code(), @"RosterView\.Draw\s*\(").Count >= 2,
            "One or both roster views stopped going through RosterView.");
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
    // files being read are the right ones. This names something only the host surface contains and
    // something only the joiner surface contains, so it fails if EITHER stops being read — which is
    // the failure the move to a directory scan newly makes possible.
    [Fact]
    public void TheReaderIsReadingBothHalvesOfTheSessionWindow()
    {
        var code = Code();

        Assert.NotEmpty(code);
        Assert.Contains("private void DrawHosting()", code, StringComparison.Ordinal);
        Assert.Contains("class JoinFlowView", code, StringComparison.Ordinal);
    }

    // BUG-48's lesson, applied to THIS guard (DMXENG-15). Until the split this read one named file,
    // so every "exactly one" above was only ever true OF SessionWindow.cs while claiming to be true
    // of the codebase — a second role label or a second renderer one file along would have passed.
    // Nothing was actually wrong on main; the guard was true by accident rather than by coverage.
    // Its sibling CopiedCodePastesIntoTheJoinFieldTests already made exactly this correction.
    [Fact]
    public void TheGuardReadsEveryWindowRatherThanOneNamedFile()
    {
        var scanned = WindowSources().Select(Path.GetFileName).ToList();
        var onDisk = Directory.EnumerateFiles(WindowsDirectory(), "*.cs").Select(Path.GetFileName).ToList();

        // Derived from disk on both sides, so a window added tomorrow is covered without anyone
        // remembering to add it here.
        Assert.Equal(
            onDisk.OrderBy(name => name, StringComparer.Ordinal),
            scanned.OrderBy(name => name, StringComparer.Ordinal));

        Assert.True(
            scanned.Count > 1,
            $"The guard scanned {scanned.Count} file(s). It must read every window, or its claim that "
            + "the roster has one renderer is false one file along.");
    }

    // THE OTHER HALF OF THE HEADING GUARD, and the half whose absence defeated the last one.
    // The value lives in Core and is tested there; what no value test can see is whether the window
    // USES it. The previous guard read the constant's text and the Code Reviewer beat it in one
    // line -- constant left honest, literal passed to the draw call, all 775 green. So this asserts
    // the draw call renders RosterHeading.Text and that no literal heading sits beside it.
    //
    // WHAT THIS IS AND IS NOT (BUG-66). The two halves of this guard are different KINDS of thing
    // and only one of them is a proof.
    //
    //   VALUE -- a proof. RosterHeading.Text is a Core constant and
    //   TheRosterHeadingClaimsOnlyWhatItShowsTests asserts over the VALUE ITSELF, so there is
    //   nothing to bypass. Widening the constant to an overclaim fails it, naming the ticket, the
    //   action and the reason. qa-1 measured that; this bug does not reach it.
    //
    //   USE -- a TEXTUAL PROXY. Everything below reads SOURCE TEXT. Contains proves the sanctioned
    //   call is PRESENT. It does not prove it is the ONLY heading drawn, and no scan of this shape
    //   can say that.
    //
    // TWO LINES DEFEAT IT, recorded so nobody has to rediscover them (qa-1, BUG-66):
    //
    //     ImGui.TextUnformatted(RosterHeading.Text);        // Contains passes
    //     ImGui.TextUnformatted("Everyone in this game:");  // DoesNotContain passes
    //
    // 22 passed, 0 failed -- and a user reads "Everyone in this game:" over a roster that
    // structurally omits the host, which is false rather than merely incomplete. Reproduced before
    // this was written.
    //
    // MEASURED AGAINST THE ASSERTIONS BELOW, every row executed rather than reasoned about:
    //     sanctioned call REPLACED by the banned literal      -> CAUGHT      (1 failed)
    //     sanctioned call REPLACED via a local, none in call  -> CAUGHT      (1 failed)
    //     SECOND heading using the BANNED literal             -> CAUGHT      (1 failed)
    //     second heading, banned substring held in a local    -> CAUGHT      (1 failed)
    //     SECOND heading, a DIFFERENT overclaiming literal    -> NOT CAUGHT  (22 passed, 0 failed)
    //     the same, held in a local rather than inline        -> NOT CAUGHT  (22 passed, 0 failed)
    //
    // So the boundary is exact, and narrower than "it bans one literal": Contains catches every
    // REPLACEMENT of the sanctioned call, and DoesNotContain catches the banned substring ANYWHERE
    // in the file, inline or by way of a local. NEITHER CATCHES AN ADDED HEADING THAT AVOIDS THE
    // BANNED SUBSTRING -- addition is the whole gap, and indirection has nothing to do with it.
    // What this establishes is "the sanctioned call is present and the banned substring is absent",
    // NOT "the heading a user reads is the Core value."
    //
    // NO FIFTH SCAN, DELIBERATELY. This family has had four guards -- constant value, line match,
    // statement match, name match -- and every replacement narrowed the gap while keeping the same
    // verb: matching TEXT where the property is about what a USER SEES. Each bought exactly one
    // hop. Banning every literal passed to TextUnformatted is already considered and rejected: it
    // is false on this window's other legitimate text, so it would need an exception list, and an
    // exception list is a denylist wearing an allowlist's name. Saying "the sanctioned call is the
    // ONLY heading" requires scoping the roster region, and scoping is a parse.
    //
    // A REAL FIX ASSERTS OVER BEHAVIOUR OR OVER A PARSE -- observe what the window actually draws,
    // or read the syntax tree and follow the call. Both are larger than this file, so THE
    // END-TO-END COVERAGE IS THE IN-GAME CHECK, and it is load-bearing rather than supplementary.
    //
    // AND WHAT DELETING THIS WOULD COST, which is the half that survives someone tidying up:
    // "it is only a proxy" reads as a case for removal right up until the regression has a name.
    // DELETE THIS AND THE ORIGINAL DEFEAT COMES BACK WITH NOTHING TO NOTICE -- the Core constant
    // left honest while the window passes its own literal to the draw call, which is precisely what
    // happened and was green across the whole suite. Four of the six shapes above stop being
    // caught. A proxy that holds against four of six known defeats is worth less than a proof and
    // considerably more than no check at all. Kept exactly as it is.
    [Fact]
    public void TheWindowRendersTheCoreHeadingRatherThanALiteral()
    {
        var code = Code();

        Assert.Contains("ImGui.TextUnformatted(RosterHeading.Text)", code, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "in this session:",
            code,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The window's source with comment lines removed.</summary>
    /// <remarks>
    /// <para>
    /// Stripped because this file's own commentary names <c>DrawRoster</c> and
    /// <c>SessionRoleLabel</c> while explaining them, and a count that includes prose counts the
    /// explanation as a second implementation.
    /// </para>
    /// <para>
    /// <b>Which families this handles, said plainly so the next reader does not assume more.</b> It
    /// removes lines whose TRIMMED START is <c>//</c> — line comments and XML-doc comments. It does
    /// NOT remove block comments or a trailing comment after code. Both of those INFLATE the counts
    /// above, so their failure direction is a false FAIL, which is the safe one. The one narrow
    /// false-PASS is <c>BothSidesCallIt</c>'s <c>&gt;= 3</c>, part of which a commented-out call
    /// could supply.
    /// </para>
    /// </remarks>
    private static string Code() =>
        string.Join(
            "\n",
            WindowSources()
                .SelectMany(File.ReadAllLines)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    /// <summary>Every window's source, in a stable order.</summary>
    /// <remarks>
    /// Ordered so a failure message is the same on two machines; the assertions are counts over the
    /// concatenation and do not depend on it.
    /// </remarks>
    private static IReadOnlyList<string> WindowSources() =>
        Directory.EnumerateFiles(WindowsDirectory(), "*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static string WindowsDirectory()
    {
        var directory = Path.Combine(RepositoryRoot(), "Windows");

        Assert.True(Directory.Exists(directory), $"No Windows/ at '{directory}'.");

        return directory;
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
