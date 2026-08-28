using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.18: the copied session code is accepted <b>verbatim</b> by the join field.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two components, both correct alone, and the defect lives between them.</b> R-1.2a groups the
/// code so it can be read aloud, so the producer may reasonably copy the grouped form; the join
/// field may reasonably reject anything it was not taught to strip. A test on the copy and a test
/// on the join field can both pass while A-1.18 fails, which is why this one crosses.
/// </para>
/// <para>
/// <b>What this can and cannot hold, stated rather than left to be assumed.</b> It pins the two
/// Core halves against each other over the whole alphabet. It still cannot <i>execute</i> the
/// button: <c>DungeonMasterXIV.Tests</c> references Core alone and may never reference the plugin,
/// so <see cref="WhatTheCopyActionProduces"/> mirrors the window rather than calling it.
/// <para>
/// <b>That mirror is no longer unchecked (BUG-44), and the sentence here used to say it was.</b> It
/// read "the one link a reviewer must still check by eye" — which was true of a linked test and not
/// of a source-reading one, and an unchecked link turned out to be an undetectable one: changing the
/// button to add a label left the whole suite green at 534 passed while producing a clipboard value
/// this very file lists as rejected. <see cref="TheButtonCopiesTheDisplayedCodeAndNothingElse"/>
/// reads the window's source and closes it, the way <c>TlsBypassFenceTests</c> already does for a
/// D-1 fence from this same project.
/// </para>
/// <para>
/// What remains unheld, so the next reader does not over-trust this: the source check asserts the
/// SHAPE of the copied expression, not that <c>ToDisplayString</c> and <c>TryParse</c> agree — that
/// is what the Core-to-Core tests below are for, and the two together are what A-1.18 needs.
/// </para>
/// </remarks>
public class CopiedCodePastesIntoTheJoinFieldTests
{
    /// <summary>
    /// What the Copy button puts on the clipboard — mirrors <c>Windows/SessionWindow.cs</c>,
    /// <c>DrawHosting</c>, which calls <c>ImGui.SetClipboardText(code.ToDisplayString())</c>.
    /// </summary>
    private static string WhatTheCopyActionProduces(SessionCode code) => code.ToDisplayString();

    /// <summary>
    /// The argument the Copy button actually passes to <c>SetClipboardText</c>, read out of
    /// <c>Windows/SessionWindow.cs</c>.
    /// </summary>
    /// <remarks>
    /// <b>Reading the source is how a Core-only project reaches a plugin line (BUG-44).</b> This
    /// project references Core alone and may never reference the plugin, so nothing here can execute
    /// <see cref="WhatTheCopyActionProduces"/>'s original. That was taken to mean the link could only
    /// be checked by eye — and an unchecked link is an undetectable one: changing the button to
    /// <c>$"Session code: {code.ToDisplayString()}"</c> left the whole suite green at 534 passed,
    /// while producing a clipboard value this very file lists as one the join field rejects.
    /// <para>
    /// <c>TlsBypassFenceTests</c> in this same project already reads source with
    /// <see cref="File.ReadAllText"/> and a <see cref="Regex"/>, and is trusted for a D-1 fence. The
    /// technique is established here; it had just not been pointed at this line.
    /// </para>
    /// </remarks>
    private static string WhatTheButtonPutsOnTheClipboard()
    {
        var source = File.ReadAllText(SessionWindowSource());
        var calls = Regex.Matches(source, @"SetClipboardText\(\s*(?<argument>[^;]*?)\s*\)\s*;");

        // Exactly one, so a second copy path cannot appear beside this one unnoticed -- which is the
        // same defect one layer along, and the reason this asserts the count rather than taking the
        // first match.
        Assert.Single(calls);

        return calls[0].Groups["argument"].Value;
    }

    private static string SessionWindowSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows", "SessionWindow.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"No Windows/SessionWindow.cs above {AppContext.BaseDirectory}; the window this reads is missing.");
    }

    // BUG-44. A-1.18 requires the copied value to be accepted VERBATIM by the join field, and the
    // mirror above only asserts that of a string this test file invents. This asserts it of the
    // button.
    //
    // The shape rather than the exact text, deliberately: renaming the local is harmless and must
    // not fail, while anything that wraps, labels or concatenates the value must. A literal string
    // comparison would forbid the first as loudly as the second, and a guard that fails on a benign
    // edit gets relaxed rather than fixed.
    [Fact]
    public void TheButtonCopiesTheDisplayedCodeAndNothingElse()
    {
        var argument = WhatTheButtonPutsOnTheClipboard();

        Assert.Matches(@"^[A-Za-z_][A-Za-z0-9_]*\.ToDisplayString\(\)$", argument);
    }

    // The half that names what went wrong rather than only forbidding it: the reported break put a
    // label in front of the code, and the mutation that proved the gap was exactly that.
    [Fact]
    public void TheButtonAddsNoLabelOrPunctuationAroundTheCode()
    {
        var argument = WhatTheButtonPutsOnTheClipboard();

        Assert.DoesNotContain("\"", argument, StringComparison.Ordinal);
        Assert.DoesNotContain("+", argument, StringComparison.Ordinal);
    }

    /// <summary>
    /// What the join field accepts — mirrors <c>Windows/SessionWindow.cs</c>, <c>DrawJoining</c>,
    /// which passes the input box's contents to <see cref="SessionCode.TryParse"/> unaltered.
    /// </summary>
    private static bool WhatTheJoinFieldAccepts(string pasted, out SessionCode code) =>
        SessionCode.TryParse(pasted, out code);

    // Fails if: the copy carries display grouping the join field rejects. Over many codes rather
    // than one, because a single sample cannot distinguish "the grouping is accepted" from "this
    // code happened not to need stripping".
    [Fact]
    public void ACopiedCodePastesIntoTheJoinFieldUnaltered()
    {
        foreach (var original in Enumerable.Range(0, 200).Select(_ => SessionCodeGenerator.Next()))
        {
            var copied = WhatTheCopyActionProduces(original);

            Assert.True(
                WhatTheJoinFieldAccepts(copied, out var pasted),
                $"The join field rejected the copied code '{copied}'.");
            Assert.Equal(original, pasted);
        }
    }

    // Fails if: the join field accepts anything at all. Without this, the test above passes just as
    // happily against a TryParse that never rejects — and "the paste was accepted" would be a
    // property of the parser rather than a fact about the copy. Both spellings are ones a
    // reasonable copy action might have produced.
    [Theory]
    [InlineData("BKD 7RM")]                 // grouped with a space instead of a hyphen
    [InlineData("Session code: BKD-7RM")]   // the rendered line copied along with its label
    public void TheJoinFieldRejectsSpellingsACopyMightPlausiblyHaveProduced(string pasted)
    {
        Assert.False(WhatTheJoinFieldAccepts(pasted, out _));
    }

    // Recorded as acceptance rather than rejection, because it IS accepted: TryParse trims. A
    // clipboard that picked up a trailing newline or space still pastes, and stating that here
    // stops the theory above from reading as "anything unusual is refused".
    [Fact]
    public void SurroundingWhitespaceStillPastes()
    {
        Assert.True(WhatTheJoinFieldAccepts(" BKD-7RM\n", out var pasted));
        Assert.Equal(SessionCode.FromValid("BKD7RM"), pasted);
    }

    // Fails if: the hyphen stops being the separator on one side only. This is the specific drift
    // A-1.18 names — the copy would still "succeed" and the paste would stop working.
    [Fact]
    public void TheSeparatorTheCopyEmitsIsOneTheJoinFieldStrips()
    {
        var copied = WhatTheCopyActionProduces(SessionCode.FromValid("BKD7RM"));

        Assert.Equal("BKD-7RM", copied);
        Assert.True(WhatTheJoinFieldAccepts(copied, out _));
    }
}
