using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.9l: the picker says what resuming does not restore.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE MERGE GATE for this PR, and it guards a PROPERTY rather than fixing one.</b> The picker
/// offers resumption of a campaign whose roster is empty and will stay empty until relink lands.
/// The disclosure is the only thing making that offer honest, so a later change that quietly removed
/// it would restore the defect with nothing to notice — the sentence is not covered by any
/// behavioural test, because nothing can execute a window.
/// </para>
/// <para>
/// <b>A SOURCE SCAN, and its limit is stated rather than implied.</b> No test project references the
/// plugin. This proves the call is PRESENT and reachable on the same path as the control; it cannot
/// prove a human reads it, and it does not look at the screen. The end-to-end check is in-game.
/// </para>
/// <para>
/// <b>It deliberately does NOT assert the WORDING.</b> The sentence is the Product Owner's and they
/// may revise it; pinning the string here would make their copy un-editable without a test change.
/// The guard protects PRESENCE, the criterion protects CONTENT — see
/// <see cref="ShippedCopyMeetsItsConstraintsTests"/> for where copy itself is held.
/// </para>
/// </remarks>
public class TheResumeOfferDisclosesWhatItCannotDoTests
{
    private static string PickerSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows", "HostCampaignPicker.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        throw new InvalidOperationException("No Windows/HostCampaignPicker.cs above the test binary.");
    }

    /// <summary>The picker's body, with comment lines removed.</summary>
    /// <remarks>
    /// Stripped because this file's own reasoning names the constant while explaining it, and a scan
    /// that counted prose would pass on an explanation of a call that no longer exists.
    /// </remarks>
    private static string Drawing() =>
        string.Join(
            "\n",
            PickerSource()
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));

    // The vacuity control: every assertion below is a substring match, so if the reader returned
    // nothing they would all fail loudly while nothing proved the right file was read. This names
    // something only this file contains.
    [Fact]
    public void TheReaderIsReadingThePicker()
    {
        Assert.Contains("class HostCampaignPicker", Drawing(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePickerRendersTheDisclosure()
    {
        Assert.Contains("ImGui.TextWrapped(ResumeDisclosure)", Drawing(), StringComparison.Ordinal);
    }

    // ORDER ONLY, and that is now stated rather than implied (BUG-82). This establishes that the
    // disclosure comes after the nothing-to-resume return and before the control. IT DOES NOT
    // ESTABLISH THAT THE DISCLOSURE IS UNCONDITIONAL -- this comment used to claim it did, and
    // qa-2 wrote the mutation that proved otherwise: wrapping the call in `if (...)` moves it a few
    // characters and changes neither ordering, so all four tests passed. Nesting is what that needs
    // and it is asserted below, separately, so a failure names which of the two properties broke.
    [Fact]
    public void TheDisclosureIsDrawnWheneverThePickerIs()
    {
        var drawing = Drawing();

        var earlyReturn = drawing.IndexOf("return;", StringComparison.Ordinal);
        var disclosure = drawing.IndexOf("ImGui.TextWrapped(ResumeDisclosure)", StringComparison.Ordinal);
        var combo = drawing.IndexOf("ImGui.BeginCombo", StringComparison.Ordinal);

        Assert.True(earlyReturn >= 0 && disclosure >= 0 && combo >= 0, "The picker no longer has the shape this reads.");
        Assert.True(disclosure > earlyReturn, "The disclosure must come after the nothing-to-resume return, so a first run is unaffected.");
        Assert.True(disclosure < combo, "The disclosure must precede the control, not sit inside it — the belief forms on seeing the offer.");
    }

    // THE ASSERTION THAT MATTERS (BUG-82). The failure guarded is the disclosure surviving inside a
    // branch that stops running -- present in the file, absent from the screen -- and it is asserted
    // as NESTING because ordering cannot see it.
    //
    // WHY A TEXT SCAN IS ALLOWED TO DECIDE THIS AT ALL, since a scan sees text and reachability is
    // about execution. The property here is narrower than reachability, and the narrowing is what
    // makes it decidable: AN EARLY RETURN CANNOT VIOLATE IT. A `return` skips the control as well as
    // the disclosure, so "if the picker draws, the disclosure drew" still holds. The only way to
    // have the control draw while the disclosure does not is to put the disclosure inside a block
    // the control is not inside -- and that is nesting, which is lexical and exact.
    //
    // So this asserts the disclosure sits at the TOP STATEMENT LEVEL of Draw(), where every path
    // reaching the control has already passed it. Not "at the same depth as the combo": two
    // different branches can share a depth.
    [Fact]
    public void TheDisclosureIsNotNestedInsideACondition()
    {
        var drawing = Drawing();

        var disclosure = drawing.IndexOf("ImGui.TextWrapped(ResumeDisclosure)", StringComparison.Ordinal);
        Assert.True(disclosure >= 0, "The picker no longer has the shape this reads.");

        Assert.Equal(TopLevelOfDraw, DepthInDraw(drawing, disclosure));
    }

    // Directly inside Draw's body: one unclosed brace since the body opened.
    private const int TopLevelOfDraw = 1;

    /// <summary>Brace depth at <paramref name="index"/>, counted from the opening of <c>Draw</c>'s body.</summary>
    /// <remarks>
    /// <para>
    /// <b>Its limits, stated rather than implied.</b> It counts braces in the comment-stripped
    /// source, so a brace inside a string literal, a char literal, or a TRAILING comment would skew
    /// it. An interpolated hole is balanced and cancels out; a lone <c>"{"</c> would not. The
    /// failure direction is a wrong depth and therefore a false FAIL, which is the one somebody
    /// investigates.
    /// </para>
    /// <para>
    /// And it says nothing about whether <c>Draw</c> is called, or about what ImGui puts on screen.
    /// Those are the caller's business and the in-game check's respectively. This decides exactly
    /// one question, whether the statement is nested, and that question is genuinely textual.
    /// </para>
    /// </remarks>
    private static int DepthInDraw(string drawing, int index)
    {
        var method = drawing.IndexOf("public void Draw()", StringComparison.Ordinal);
        Assert.True(method >= 0 && method < index, "Draw() is not where this expects it.");

        var body = drawing.IndexOf('{', method);
        Assert.True(body >= 0 && body < index, "Draw() has no body before the statement being measured.");

        var depth = 0;

        for (var i = body; i < index; i++)
        {
            depth += drawing[i] switch { '{' => 1, '}' => -1, _ => 0 };
        }

        return depth;
    }

    // The sentence must actually BE a sentence. A constant emptied to silence a failing scan would
    // pass every assertion above while disclosing nothing.
    //
    // Read out of the SOURCE rather than referenced, because the test project cannot reference the
    // plugin — which is the same reason every assertion here is textual. Asserting its LENGTH and
    // not its WORDS is deliberate: the Product Owner may revise the sentence, and a guard that
    // pinned the string would make their copy un-editable without a test change.
    [Fact]
    public void TheDisclosureSaysSomething()
    {
        // Bounded at the statement's semicolon. An unbounded read runs on into the NEXT string
        // literal in the file and reports a constant that is longer than it is — which is how a
        // guard against an emptied constant would itself stop working.
        var body = Drawing()[Drawing().IndexOf("ResumeDisclosure =", StringComparison.Ordinal)..];
        var statement = body[..body.IndexOf(';', StringComparison.Ordinal)];

        var literal = string.Concat(
            Regex.Matches(statement, "\"([^\"]*)\"").Select(match => match.Groups[1].Value));

        Assert.False(string.IsNullOrWhiteSpace(literal), "ResumeDisclosure is empty.");
        Assert.True(literal.Length > 40, $"A one-word disclosure is not one. Got '{literal}'.");
    }
}
