using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.7d and A-1.7e over one corpus: every string this product ships to a user.
/// </summary>
/// <remarks>
/// <para>
/// <b>One instrument for two criteria, because two sweeps over one corpus diverge.</b> A-1.7e asks
/// that engineering-authored copy meets R-1.7a's constraints; A-1.7d asks that no shipped string
/// still describes behaviour a decision reversed. They differ only in which phrasings are refused,
/// so they share the corpus and the extractor and part company at the last step.
/// </para>
/// <para>
/// <b>A property, not a pin, and that is the ruling rather than a preference.</b> R-1.7a settles it:
/// <i>"Byte-pin where the words ARE the decision; assert a property where the CLASS is the
/// decision."</i> The six engineering-authored strings are one decision — never tell a user they are
/// protected when nobody checked — and an enumeration binds the six someone listed while staying
/// silent on the seventh. <c>TheAdmissionPromptCopyIsTheRuledCopyTests</c> is the other instrument
/// and pins the strings R-1.7a quotes.
/// </para>
/// <para>
/// <b>What this cannot do, stated rather than implied.</b> The refused phrasings are TRANSCRIBED
/// from R-1.7a, because the PRD lives under <c>.claude/</c>, which is gitignored — a test reading it
/// would pass here and fail on every clean checkout, exactly as
/// <c>TheAdmissionPromptCopyIsTheRuledCopyTests</c> records. So the LIST grows by hand when a
/// decision reverses. What is derived, and what makes this a universal rather than a pin, is the
/// CORPUS: a string added tomorrow is swept without anyone remembering to add it.
/// </para>
/// </remarks>
public class ShippedCopyMeetsItsConstraintsTests
{
    /// <summary>
    /// R-1.7a's forbidden phrasings, transcribed. "Each is false under D-8 and the last is false
    /// even with encryption."
    /// </summary>
    private static readonly string[] ForbiddenPhrasings =
    {
        "anonymous",
        "private",
        "we can't see anything",
        "no one can see your session",
    };

    /// <summary>
    /// Phrasings a decision retired. A shipped string still carrying one describes behaviour the
    /// product no longer has (A-1.7d).
    /// </summary>
    /// <remarks>
    /// <b>"not a character name"</b> — the 2026-08-27 reversal (SQ-34). It contradicted R-1.3e from
    /// the moment R-1.3e was decided, and was found by someone else hours later rather than by the
    /// reversal itself. <c>TheDisclosureNoLongerDeniesThatANameIsShown</c> pins it for the one
    /// constant it was found in; this refuses it across every shipped string, including ones written
    /// after that test.
    /// </remarks>
    private static readonly string[] RetiredPhrasings =
    {
        "not a character name",
    };

    /// <summary>
    /// The constants holding copy R-1.7a QUOTES. Everything else is engineering-authored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two criteria have different corpora and I had them as one.</b> A-1.7d governs
    /// <i>every shipped string</i> — the SQ-34 defect was in ruled copy, so exempting it would
    /// exempt the case that produced the criterion. A-1.7e governs <i>engineering-authored</i>
    /// strings only, by its own words, and applying it to the Product Owner's text refuses copy an
    /// engineer may not alter anyway.
    /// </para>
    /// <para>
    /// <b>Enumerating the RULED side is what keeps the other side universal.</b> This list is small,
    /// changes only when a decision changes it, and is the same set A-1.7c byte-pins. Everything not
    /// on it is engineering-authored <i>by default</i> — so a string added tomorrow gets the
    /// stricter constraint without anyone remembering to classify it. Enumerating the other side
    /// would have been the pin-the-six mistake R-1.7a settled against.
    /// </para>
    /// </remarks>
    private static readonly string[] RuledConstants =
    {
        "CodeDisclosure",
        "AdmissionDisclosure",
        "WhatThisPluginKnows",
    };

    /// <summary>
    /// Claims that a session is protected, which fail unless the sentence denies them (R-1.7a, D-8).
    /// </summary>
    /// <remarks>
    /// <b>Negation is checked because the correct copy uses these words.</b> <c>UnverifiedWarning</c>
    /// ships "This session is <i>not</i> protected against someone sitting in the middle of it" —
    /// refusing the phrase outright would fail the string that states the limitation properly, which
    /// is the opposite of the criterion. R-1.7a chose this direction knowingly: <i>"The failure mode
    /// of a byte-pin is silence on the new case; the failure mode of a property is a false positive
    /// someone must argue with. The second is recoverable in review. The first ships."</i>
    /// </remarks>
    private static readonly string[] ProtectionClaims =
    {
        "protected",
        "secure",
        "safe",
    };

    // NOT "is encrypted", and the sweep is what taught me the difference. It flagged the settings
    // disclosure -- "Your session is encrypted end to end... encryption hides what you say, not that
    // you are talking" -- which is R-1.9's REQUIRED copy and true: D-11 encryption is a fact about
    // the transport, and the string already qualifies it. The unchecked thing is the fingerprint
    // comparison, so "protected" is the overclaim and "encrypted" is not. R-1.7a predicted exactly
    // this: "the failure mode of a property is a false positive someone must argue with... the
    // second is recoverable in review." Argued with, and recovered.

    // A-1.7e. Fails on any shipped string using a phrasing R-1.7a forbids.
    [Fact]
    public void NoEngineeringAuthoredStringUsesAForbiddenPhrasing()
    {
        AssertNothingViolates(EngineeringAuthoredCopy(), ForbiddenPhrasings);
    }

    // A-1.7d. Fails on any shipped string still describing behaviour a decision reversed.
    // EVERY shipped string, ruled copy included: the reversal that produced this criterion stranded
    // a string R-1.7a itself quoted, so exempting ruled copy would exempt the original case.
    [Fact]
    public void NoShippedStringStillDescribesReversedBehaviour()
    {
        AssertNothingViolates(ShippedCopy(), RetiredPhrasings);
    }

    // A-1.7e's other half: the D-8 overclaim. An UNDENIED protection claim fails.
    [Fact]
    public void NoShippedStringClaimsProtectionThatWasNeverChecked()
    {
        var offenders = EngineeringAuthoredCopy()
            .Where(copy => UndeniedProtectionClaimsIn(copy.Text).Any())
            .Select(copy => $"{copy.File}: \"{Excerpt(copy.Text)}\"")
            .ToList();

        Assert.True(offenders.Count == 0, "Shipped copy claims protection nobody checked:\n" + string.Join("\n", offenders));
    }

    // THE POSITIVE CONTROL, and without it none of the three above is a test. They pass on today's
    // main — 106 shipped literals, no violations — so a sweep that could not fail would look
    // identical. Each refusal is fed a string that must trip it, through the SAME predicates the
    // sweep uses rather than a copy of them.
    [Theory]
    [InlineData("Your session is anonymous.")]
    [InlineData("This is private between the two of you.")]
    [InlineData("Relax - we can't see anything you send.")]
    [InlineData("This request shows a code, not a character name.")]
    public void TheSweepRefusesAStringThatViolatesIt(string violating)
    {
        var caught = ForbiddenPhrasingsIn(violating, ForbiddenPhrasings)
            .Concat(ForbiddenPhrasingsIn(violating, RetiredPhrasings))
            .ToList();

        Assert.True(caught.Count > 0, $"The sweep passed a string that should fail it: \"{violating}\"");
    }

    // The overclaim half needs its own control AND its own negative one: the phrase must fail
    // undenied and pass when denied, or the check is either useless or refuses correct copy.
    [Fact]
    public void TheOverclaimCheckRefusesAClaimAndAllowsItsDenial()
    {
        Assert.NotEmpty(UndeniedProtectionClaimsIn("This session is protected end to end."));
        Assert.NotEmpty(UndeniedProtectionClaimsIn("Your session is fully protected."));
        Assert.NotEmpty(UndeniedProtectionClaimsIn("This keeps your group safe."));
        Assert.Empty(UndeniedProtectionClaimsIn("This session is not protected against someone in the middle."));

        // The limit, asserted rather than described so it cannot quietly widen: denial is only seen
        // within a short window before the claim. "Nothing here is ever secure" IS a denial and this
        // refuses it -- a false positive, in the direction R-1.7a chose knowingly. Recorded as a
        // failing-shaped fact so that anyone who widens the window has to change this line and say
        // why, rather than discovering the behaviour by accident.
        Assert.NotEmpty(UndeniedProtectionClaimsIn("Nothing here is ever secure without the code check."));
    }

    // BUG-48's lesson: a guard that claims a property of the CODEBASE must read the codebase rather
    // than a list of files someone maintained. A window added tomorrow is swept because the set is
    // derived from disk on both sides.
    [Fact]
    public void TheSweepReadsEveryWindowOnDisk()
    {
        var scanned = SourcesSwept().Select(Path.GetFileName).ToList();
        var windows = Directory.EnumerateFiles(WindowsDirectory(), "*.cs").Select(Path.GetFileName);

        Assert.All(windows, window => Assert.Contains(window, scanned));
        Assert.True(scanned.Count > 1, "The sweep narrowed to one file; it claims to cover the shipped copy.");
    }

    // And the extractor must actually find copy, or every sweep above passes over an empty corpus —
    // the vacuous-instrument shape. Named strings from three different files, in two declaration
    // forms: const in a window, and a switch arm in Core.
    [Theory]
    [InlineData("Your session code is not a secret")]          // SessionWindow, const
    [InlineData("The name shown is chosen by the requester")]  // AdmissionPromptView, const
    [InlineData("This name is not checked by anything")]       // ConfigWindow, const
    [InlineData("The relay is not responding")]                // SessionFailure, switch arm
    public void TheCorpusContainsCopyFromEveryShapeItMustCover(string fragment)
    {
        Assert.Contains(ShippedCopy(), copy => copy.Text.Contains(fragment, StringComparison.Ordinal));
    }

    // The classification is load-bearing and silently does nothing if it is wrong: a typo in
    // RuledConstants matches no declaration, every string falls into the engineering-authored
    // corpus, and the A-1.7e sweep quietly reverts to refusing the Product Owner's copy -- while
    // still passing, because it passes today. So the split is asserted in both directions.
    [Fact]
    public void TheRuledCopyIsClassifiedAsRuledAndTheRestIsNot()
    {
        var ruledFound = ShippedCopy()
            .Where(copy => RuledConstants.Contains(copy.Name, StringComparer.Ordinal))
            .Select(copy => copy.Name)
            .Distinct()
            .ToList();

        // Every name on the list matched a real declaration. A name that matches nothing is a typo
        // that reads as a working exclusion.
        Assert.Equal(
            RuledConstants.OrderBy(name => name, StringComparer.Ordinal),
            ruledFound.OrderBy(name => name, StringComparer.Ordinal));

        // And the engineering-authored corpus excludes them while keeping the six A-1.7e names.
        var authored = EngineeringAuthoredCopy().Select(copy => copy.Name).ToList();

        Assert.DoesNotContain("CodeDisclosure", authored);
        Assert.DoesNotContain("AdmissionDisclosure", authored);
        Assert.DoesNotContain("WhatThisPluginKnows", authored);

        foreach (var six in new[]
                 {
                     "CompareOutOfBand", "UnverifiedWarning", "ReadYourCodeAloud",
                     "NoCodeToCompare", "AdmittedUncompared", "CodeChangedWarning",
                 })
        {
            Assert.Contains(six, authored);
        }
    }

    private static void AssertNothingViolates(
        IReadOnlyList<(string File, string Name, string Text)> corpus, string[] phrasings)
    {
        var offenders = corpus
            .SelectMany(copy => ForbiddenPhrasingsIn(copy.Text, phrasings)
                .Select(hit => $"{copy.File} {copy.Name}: \"{hit}\" in \"{Excerpt(copy.Text)}\""))
            .ToList();

        Assert.True(offenders.Count == 0, "Shipped copy uses refused phrasing:\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<string> ForbiddenPhrasingsIn(string text, string[] phrasings) =>
        phrasings.Where(phrasing => text.Contains(phrasing, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> UndeniedProtectionClaimsIn(string text) =>
        ProtectionClaims.Where(claim =>
        {
            var at = text.IndexOf(claim, StringComparison.OrdinalIgnoreCase);

            // The bare word, not "is protected". A probe caught this: a string reading "fully
            // protected" slipped an "is protected" pattern entirely, so the check would have missed
            // the overclaim it exists for while looking like it covered it.
            return at >= 0 && !IsDenied(text, at);
        });

    /// <summary>Whether the words before a protection claim deny it.</summary>
    /// <remarks>
    /// <b>Backwards, and that was a bug in the first version.</b> It looked FORWARD from the claim,
    /// which cannot see the "not" in "is not protected" — the denial always precedes the word. The
    /// correct copy depends on this: <c>UnverifiedWarning</c> ships "This session is not protected
    /// against someone sitting in the middle of it", and refusing that string would refuse the one
    /// that states the limitation properly. The window is short so a "not" about something else
    /// earlier in a long sentence does not excuse a claim later in it.
    /// </remarks>
    private static bool IsDenied(string text, int claimAt)
    {
        var from = Math.Max(0, claimAt - 12);
        var before = text[from..claimAt];

        return before.Contains("not", StringComparison.OrdinalIgnoreCase)
            || before.Contains("never", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every string literal this product ships, with the file and declaration it came from.</summary>
    /// <remarks>
    /// <b>No length or shape filter, deliberately.</b> Filtering to "sentences" would be a second
    /// place for a violating string to hide — "anonymous" alone is nine characters and would pass a
    /// plausible one. The cost is that identifiers and format fragments are swept too; they contain
    /// no refused phrasing, so the cost is nothing.
    /// </remarks>
    private static IReadOnlyList<(string File, string Name, string Text)> ShippedCopy() =>
        SourcesSwept()
            .SelectMany(source => LiteralsIn(source)
                .Select(literal => (File: Path.GetFileName(source), literal.Name, literal.Text)))
            .ToList();

    /// <summary>The shipped copy R-1.7a does not quote — what A-1.7e governs.</summary>
    private static IReadOnlyList<(string File, string Name, string Text)> EngineeringAuthoredCopy() =>
        ShippedCopy().Where(copy => !RuledConstants.Contains(copy.Name, StringComparer.Ordinal)).ToList();

    private static IEnumerable<(string Name, string Text)> LiteralsIn(string source)
    {
        // Comment lines are stripped first: this file's own commentary quotes refused phrasings, and
        // so does the source it reads — SessionFailure.cs explains BUG-49 by quoting the sentence it
        // removed. Sweeping commentary would refuse the explanation of the fix.
        var body = string.Join(
            "\n",
            File.ReadAllLines(source).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        // Which declaration each literal sits in, so the ruled constants can be told from the rest.
        // A literal outside any of them — a switch arm in SessionFailure.cs, for instance — is
        // engineering-authored, which is the safe default: it gets the stricter constraint.
        var declarations = Regex
            .Matches(body, @"(?:const\s+string|string\[\])\s+(?<name>\w+)\s*=(?<body>.*?);", RegexOptions.Singleline)
            .Select(match => (Name: match.Groups["name"].Value, match.Groups["body"].Index, match.Groups["body"].Length))
            .ToList();

        foreach (Match literal in Regex.Matches(body, @"""(?<literal>(?:[^""\\]|\\.)*)"""))
        {
            var owner = declarations.FirstOrDefault(
                declaration => literal.Index >= declaration.Index
                    && literal.Index < declaration.Index + declaration.Length);

            yield return (owner.Name ?? "(inline)", literal.Groups["literal"].Value);
        }
    }

    /// <summary>
    /// The files that carry user-facing copy: every window, plus the failure sentences in Core.
    /// </summary>
    /// <remarks>
    /// <b>The windows are derived; the Core file is named, and that is the residual gap.</b> Nothing
    /// marks a string as user-facing, so the boundary is drawn by hand. Copy added to a NEW Core file
    /// would not be swept — <see cref="TheSweepReadsEveryWindowOnDisk"/> cannot catch that, and no
    /// test here can. Said plainly so the next reader does not over-trust it.
    /// </remarks>
    private static IReadOnlyList<string> SourcesSwept()
    {
        var root = RepositoryRoot();

        return Directory.EnumerateFiles(Path.Combine(root, "Windows"), "*.cs")
            .Append(Path.Combine(root, "src", "DungeonMasterXIV.Core", "Net", "SessionFailure.cs"))
            .Where(File.Exists)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string WindowsDirectory() => Path.Combine(RepositoryRoot(), "Windows");

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "SessionWindow.cs")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"No repository root above {AppContext.BaseDirectory}; the copy this sweeps is missing.");
    }

    private static string Excerpt(string text) => text.Length <= 70 ? text : text[..70] + "...";
}
