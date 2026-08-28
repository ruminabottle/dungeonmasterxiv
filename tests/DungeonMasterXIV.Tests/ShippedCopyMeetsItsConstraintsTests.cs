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
        AssertNothingViolates(ShippedCopyCorpus.ShippedCopy(), RetiredPhrasings);
    }

    // A-1.7e's other half: the D-8 overclaim. An UNDENIED protection claim fails.
    [Fact]
    public void NoShippedStringClaimsProtectionThatWasNeverChecked()
    {
        var offenders = EngineeringAuthoredCopy()
            .Where(copy => UndeniedProtectionClaimsIn(copy.Text).Any())
            .Select(copy => $"{copy.File}: \"{ShippedCopyCorpus.Excerpt(copy.Text)}\"")
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

        // THE SCANNING HOLE, and the second instance of one shape. The first version read only the
        // FIRST occurrence, so a denied first use dropped the claim word and every later use went
        // unexamined -- deny once, then reassure freely. Not contrived: AdmittedUncompared already
        // ships "not protected against someone sitting in the middle of it", and appending
        // reassurance is the natural next edit. The matching half had the same shape ("fully
        // protected" slipping an "is protected" pattern); this is the scanning half.
        Assert.NotEmpty(UndeniedProtectionClaimsIn(
            "This session is not protected against a stranger, but your messages are protected."));

        // The mirror, so the fix cannot overshoot into flagging every denial that repeats itself.
        Assert.Empty(UndeniedProtectionClaimsIn(
            "This session is not protected against a stranger and never protected against the relay."));

        // The limit, asserted rather than described so it cannot quietly widen: denial is only seen
        // within a short window before the claim. "Nothing here is ever secure" IS a denial and this
        // refuses it -- a false positive, in the direction R-1.7a chose knowingly. Recorded as a
        // failing-shaped fact so that anyone who widens the window has to change this line and say
        // why, rather than discovering the behaviour by accident.
        Assert.NotEmpty(UndeniedProtectionClaimsIn("Nothing here is ever secure without the code check."));

        // THE COUPLING, and it is the reason the line above must not be "fixed" on its own.
        // "not" is matched as a SUBSTRING, so any word containing it reads as a denial. "another"
        // is the live example: this claim is real and goes UNFLAGGED.
        Assert.Empty(UndeniedProtectionClaimsIn("Use another protected channel."));

        // The two defects partially cancel BY LUCK. "Note that", "Nothing" and "cannot" all contain
        // "not" too, and all three still flag correctly -- but only because the 12-character window
        // usually puts them out of reach, not because anything distinguishes them.
        Assert.NotEmpty(UndeniedProtectionClaimsIn("Note that this session is protected by the code."));

        // So WIDENING THE WINDOW to cure the false positive two assertions up would pull those
        // substring matches INTO range and open false negatives here. Anyone fixing either one must
        // change these lines together and say what happened to the other. Matching whole words would
        // decouple them; that is a change to the check's semantics and was not made under a denial.
    }

    // BUG-48's lesson: a guard that claims a property of the CODEBASE must read the codebase rather
    // than a list of files someone maintained. A window added tomorrow is swept because the set is
    // derived from disk on both sides.
    [Fact]
    public void TheSweepReadsEveryWindowOnDisk()
    {
        var scanned = ShippedCopyCorpus.SourcesSwept().Select(Path.GetFileName).ToList();
        var windows = Directory.EnumerateFiles(ShippedCopyCorpus.WindowsDirectory(), "*.cs").Select(Path.GetFileName);

        Assert.All(windows, window => Assert.Contains(window, scanned));
        Assert.True(scanned.Count > 1, "The sweep narrowed to one file; it claims to cover the shipped copy.");
    }

    // The enumerated half's guard, and the derived half already had one.
    // TheSweepReadsEveryWindowOnDisk proves a window cannot go unswept; nothing proved anything
    // about the named Core files, which is where DisplayName.Unstated hid.
    //
    // Asserting the named files appear in ShippedCopyCorpus.SourcesSwept would be VACUOUS -- ShippedCopyCorpus.SourcesSwept concatenates
    // that same list, so it is true by construction and could never fail. What is not vacuous is
    // that each named file actually YIELDS COPY: it exists, the extractor parses it, and it
    // contributes at least one literal. That fails if a file is listed but empty, if it is renamed
    // so the throw fires, or if the extractor stops understanding its declaration form.
    [Fact]
    public void EveryNamedCoreFileContributesCopyToTheCorpus()
    {
        var corpus = ShippedCopyCorpus.ShippedCopy();

        Assert.NotEmpty(ShippedCopyCorpus.NamedCoreCopyFiles);
        Assert.All(ShippedCopyCorpus.NamedCoreCopyFiles, relative =>
        {
            var file = Path.GetFileName(relative);
            Assert.True(
                corpus.Any(copy => string.Equals(copy.File, file, StringComparison.Ordinal)),
                $"{file} is named as carrying user-facing copy but contributed no literal to the "
                + "corpus, so the sweep covers less than the list claims.");
        });
    }

    // And the missing-file path is the one that used to be silent. Probed rather than assumed: a
    // name that is not on disk must throw and must NAME the file, because the failure it replaces
    // was a corpus that quietly shrank while every sweep kept passing.
    [Fact]
    public void ANamedCoreFileThatIsNotOnDiskIsRefusedByName()
    {
        var root = ShippedCopyCorpus.RepositoryRoot();
        var absent = Path.Combine(root, "src", "DungeonMasterXIV.Core", "Net", "NoSuchCopyFile.cs");

        var thrown = Assert.Throws<FileNotFoundException>(
            () => ShippedCopyCorpus.SweptSources(root, new[] { Path.Combine("src", "DungeonMasterXIV.Core", "Net", "NoSuchCopyFile.cs") }));

        Assert.Contains("NoSuchCopyFile.cs", thrown.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(absent), "The probe file must not exist, or this asserts nothing.");
    }

    // And the extractor must actually find copy, or every sweep above passes over an empty corpus —
    // the vacuous-instrument shape. Named strings from three different files, in two declaration
    // forms: const in a window, and a switch arm in Core.
    [Theory]
    [InlineData("Your session code is not a secret")]          // SessionWindow, const
    [InlineData("The name shown is chosen by the requester")]  // AdmissionPromptView, const
    [InlineData("This name is not checked by anything")]       // ConfigWindow, const
    [InlineData("The relay is not responding")]                // SessionFailure, switch arm
    [InlineData("a player who gave no name")]                  // DisplayName, const -- the missed one
    public void TheCorpusContainsCopyFromEveryShapeItMustCover(string fragment)
    {
        Assert.Contains(ShippedCopyCorpus.ShippedCopy(), copy => copy.Text.Contains(fragment, StringComparison.Ordinal));
    }

    // The classification is load-bearing and silently does nothing if it is wrong: a typo in
    // RuledConstants matches no declaration, every string falls into the engineering-authored
    // corpus, and the A-1.7e sweep quietly reverts to refusing the Product Owner's copy -- while
    // still passing, because it passes today. So the split is asserted in both directions.
    [Fact]
    public void TheRuledCopyIsClassifiedAsRuledAndTheRestIsNot()
    {
        var ruledFound = ShippedCopyCorpus.ShippedCopy()
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

    /// <summary>The shipped copy R-1.7a does not quote — what A-1.7e governs.</summary>
    /// <remarks>
    /// Stays with the constraints rather than the corpus: which names are RULED is a decision about
    /// what copy must say, and moving it next to the extractor would let a change to reading quietly
    /// change classification.
    /// </remarks>
    private static IReadOnlyList<(string File, string Name, string Text)> EngineeringAuthoredCopy() =>
        ShippedCopyCorpus.ShippedCopy()
            .Where(copy => !RuledConstants.Contains(copy.Name, StringComparer.Ordinal))
            .ToList();

    private static void AssertNothingViolates(
        IReadOnlyList<(string File, string Name, string Text)> corpus, string[] phrasings)
    {
        var offenders = corpus
            .SelectMany(copy => ForbiddenPhrasingsIn(copy.Text, phrasings)
                .Select(hit => $"{copy.File} {copy.Name}: \"{hit}\" in \"{ShippedCopyCorpus.Excerpt(copy.Text)}\""))
            .ToList();

        Assert.True(offenders.Count == 0, "Shipped copy uses refused phrasing:\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<string> ForbiddenPhrasingsIn(string text, string[] phrasings) =>
        phrasings.Where(phrasing => text.Contains(phrasing, StringComparison.OrdinalIgnoreCase));

    /// <summary>Protection claims in <paramref name="text"/> that nothing nearby denies.</summary>
    /// <remarks>
    /// <b>EVERY occurrence, and reading only the first was a bug.</b> The claim word is matched as
    /// the bare word rather than "is protected" — a probe caught a string reading "fully protected"
    /// slipping an "is protected" pattern, so the check missed the overclaim it exists for while
    /// looking like it covered it. That was the MATCHING hole. This is the SCANNING one, and it is
    /// the same shape a layer out: <c>IndexOf</c> returns a single index, so a DENIED first
    /// occurrence dropped the claim word and every later use went unexamined. "…is not protected
    /// against a stranger, but your messages are protected" passed. A claim is undenied if ANY of
    /// its occurrences is undenied, so denying it once no longer licenses asserting it afterwards.
    /// </remarks>
    private static IEnumerable<string> UndeniedProtectionClaimsIn(string text) =>
        ProtectionClaims.Where(claim => OccurrencesOf(claim, text).Any(at => !IsDenied(text, at)));

    private static IEnumerable<int> OccurrencesOf(string claim, string text)
    {
        for (var at = text.IndexOf(claim, StringComparison.OrdinalIgnoreCase);
             at >= 0;
             at = text.IndexOf(claim, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            yield return at;
        }
    }

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

}
