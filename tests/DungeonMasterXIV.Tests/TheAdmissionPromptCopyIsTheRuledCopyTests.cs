using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.7a's admission-prompt copy, shipped byte-for-byte (BUG-52).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the bytes and not the meaning.</b> R-1.7a's strings are product claims: <i>"Engineers do
/// not draft these... a PR may not substitute its own."</i> A test asserting a sentence "conveys the
/// same thing" would license exactly the substitution the requirement forbids, and it is what made
/// the previous defect invisible — the shipped string was <b>byte-identical to what R-1.7a then
/// said</b>, so the code was faithful and the specification was the thing that was false.
/// </para>
/// <para>
/// <b>Read from source, because no test can reach the constant.</b> It is a <c>private const</c> in
/// <c>Windows/</c>, and this project references Core alone and may never reference the plugin. The
/// technique is the one <c>TlsBypassFenceTests</c> established for a D-1 fence and BUG-44 pointed at
/// the Copy button.
/// </para>
/// <para>
/// <b>What this does NOT discharge: A-1.7c.</b> That criterion asks for a mechanical comparison
/// against R-1.7a itself. The PRD lives under <c>.claude/</c>, which is <b>gitignored and untracked</b>
/// — a test reading it would pass here and fail on every clean checkout. So the reviewed text below is
/// a <i>transcription</i> of R-1.7a, and the two can only drift if somebody edits both. That forces a
/// reading, which is the point; it is not the same as comparing to the source of truth, and saying so
/// is the difference between a guard and a claim about one.
/// </para>
/// </remarks>
public class TheAdmissionPromptCopyIsTheRuledCopyTests
{
    // R-1.7a as of SQ-34, 2026-08-28. Transcribed, then verified byte-for-byte against the PRD, the
    // Spec Owner's ruling and the Product Owner's upstream answer before this was written.
    private const string RuledDisclosure =
        "The name shown is chosen by the requester, not proof of who they are - the code is. Only admit "
        + "people you arranged to play with.";

    // Unchanged by SQ-34 and still correct. Present so that "the fingerprint copy survives" is
    // asserted rather than assumed: a disclosure rewrite that quietly dropped either of these would
    // de-emphasise the fingerprint, which D-8's amendment makes approve-blocking.
    private const string RuledCompareOutOfBand =
        "Ask the joining player to read their code back to you over voice or chat, and confirm it "
        + "matches. Do not ask them for it through the plugin - a channel someone has tampered with "
        + "cannot prove it has not been tampered with.";

    private const string RuledUnverifiedWarning =
        "Admitted without the code being compared. This session is not protected against someone "
        + "sitting in the middle of it.";

    [Theory]
    [InlineData("AdmissionDisclosure", RuledDisclosure)]
    [InlineData("CompareOutOfBand", RuledCompareOutOfBand)]
    [InlineData("UnverifiedWarning", RuledUnverifiedWarning)]
    // The parameter is named for the CONSTRAINT, not for a pin. It was "ruled:", which asserted a
    // provenance two of these three do not have: SQ-38 settled that R-1.7a governs only the strings
    // it QUOTES, and CompareOutOfBand and UnverifiedWarning are engineering-authored under its
    // constraints instead. They are byte-pinned here because they were reviewed and must not drift,
    // which is a different claim from having been ruled -- and A-1.7e is what actually holds their
    // class, in ShippedCopyMeetsItsConstraintsTests.
    public void TheShippedStringIsTheRuledString(string constant, string required)
    {
        Assert.Equal(required, ShippedConstant(constant));
    }

    // The defect this replaces, named so a revert is loud rather than merely different. The old
    // sentence denied the prompt showed a name while the prompt rendered "Bob (PRBCD4) is asking to
    // join" — it contradicted R-1.3e from the moment R-1.3e was decided.
    [Fact]
    public void TheDisclosureNoLongerDeniesThatANameIsShown()
    {
        Assert.DoesNotContain(
            "not a character name", ShippedConstant("AdmissionDisclosure"), StringComparison.Ordinal);
    }

    /// <summary>The concatenated literal of a named <c>private const string</c>, read from source.</summary>
    private static string ShippedConstant(string name)
    {
        var source = File.ReadAllText(AdmissionPromptSource());
        var declaration = Regex.Match(
            source,
            @"const\s+string\s+" + Regex.Escape(name) + @"\s*=(?<literal>.*?);",
            RegexOptions.Singleline);

        Assert.True(declaration.Success, $"No 'const string {name}' in Windows/AdmissionPromptView.cs.");

        var pieces = Regex.Matches(declaration.Groups["literal"].Value, "\"(?<piece>[^\"]*)\"")
            .Select(piece => piece.Groups["piece"].Value);

        return string.Concat(pieces);
    }

    private static string AdmissionPromptSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows", "AdmissionPromptView.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"No Windows/AdmissionPromptView.cs above {AppContext.BaseDirectory}; the prompt this reads is missing.");
    }
}
