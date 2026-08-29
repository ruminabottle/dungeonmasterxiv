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

    // THE ASSERTION THAT MATTERS, and it is not "the sentence exists". The failure I am guarding is
    // the disclosure surviving inside a branch that stops running — drawn only once a campaign is
    // chosen, or only on some other condition — so it is present in the file and absent from the
    // screen. It must sit ABOVE the early return's guard and ABOVE the combo, on the same
    // unconditional path as the control itself: if the picker draws, the sentence draws.
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
