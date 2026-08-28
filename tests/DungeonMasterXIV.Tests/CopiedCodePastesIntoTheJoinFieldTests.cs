using System.Linq;
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
/// Core halves against each other over the whole alphabet. It does <i>not</i> reach the button:
/// <c>DungeonMasterXIV.Tests</c> references Core alone and may never reference the plugin, so no
/// test in this repository can execute <c>SessionWindow</c>. <see cref="WhatTheCopyActionProduces"/>
/// therefore MIRRORS the window rather than calling it — the one link a reviewer must still check
/// by eye, named here so it is greppable instead of invisible.
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
