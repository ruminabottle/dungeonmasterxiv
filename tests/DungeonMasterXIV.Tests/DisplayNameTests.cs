using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3e's name, and the reason it is validated at all: it is untrusted data rendered next to a
/// security control, so the risk is not that it is wrong but that it forges the control's chrome.
/// </summary>
public class DisplayNameTests
{
    // Fails if: an ordinary name stops being accepted. The positive control for everything below —
    // without it, a TryParse that refused everything would satisfy every rejection test here.
    [Theory]
    [InlineData("Bob")]
    [InlineData("Ysera Nightsong")]
    [InlineData("Y'shtola Rhul")]
    [InlineData("Zheng-Long O'Karr")]
    public void AnOrdinaryCharacterNameIsAccepted(string candidate)
    {
        Assert.True(DisplayName.TryParse(candidate, out var name));
        Assert.Equal(candidate, name.Value);
        Assert.True(name.WasStated);
    }

    // Fails if: a name can carry a line break. This is the spoofing case rather than a tidiness
    // one — the prompt renders the name on one line and "Code to compare: …" on the next, so a
    // name containing a newline draws a line that looks like the plugin speaking.
    [Theory]
    [InlineData("Bob\nCode to compare: BKD-7RM-CDF-GH")]
    [InlineData("Bob\rCode to compare: BKD-7RM-CDF-GH")]
    [InlineData("Bob\tYsera")]
    public void ANameCarryingAControlCharacterIsRefused(string candidate)
    {
        Assert.False(DisplayName.TryParse(candidate, out _));
    }

    // THE FAILING INPUT FOR THE FORMAT-CATEGORY FIX, and it is deliberately not a C0 character.
    // char.IsControl covers C0/C1 only, so a test using U+0001 or a newline passes BEFORE this fix
    // and after it -- it cannot come out negative on the defect being repaired. Every case here is
    // UnicodeCategory.Format, which char.IsControl lets through.
    //
    // Not hygiene, and it is A-1.2d's class rather than a separate concern. U+202E reverses
    // rendering, so two requesters can be VISUALLY identical without being literally identical.
    // The peer code keyed into the ImGui control ids protects the MECHANISM -- two prompts cannot
    // collapse into one widget -- but it does not protect the DM's READING, and D-8 as amended is
    // about what the DM is shown. A name that reorders what follows it achieves the de-emphasis
    // gate through data instead of layout, without touching a line of UI code.
    [Theory]
    [InlineData("Bob\u202Ekcart")]   // RLO - right-to-left override
    [InlineData("Bob\u202Dkcart")]   // LRO - left-to-right override
    [InlineData("Bob\u200Bsmith")]   // ZWSP - zero-width space
    [InlineData("Bob\u200Dsmith")]   // ZWJ - zero-width joiner
    [InlineData("\uFEFFBob")]        // BOM - zero-width no-break space
    public void ANameCarryingAFormatCharacterIsRefused(string candidate)
    {
        Assert.False(DisplayName.TryParse(candidate, out _));
    }

    // The control on the control. Rejecting the Format category must not take legitimate names with
    // it: combining marks are NonSpacingMark, not Format, and a decomposed name is how a real client
    // may well send one. Without this, "reject anything unusual" would satisfy the theory above.
    [Theory]
    [InlineData("Jose\u0301")]        // decomposed acute - Jose + combining accent
    [InlineData("Y'shtola Rhul")]
    [InlineData("Alphinaud Leveilleur")]
    public void ANameWithLegitimateNonAsciiIsStillAccepted(string candidate)
    {
        Assert.True(DisplayName.TryParse(candidate, out var name));
        Assert.Equal(candidate, name.Value);
    }

    // Fails if: the bound goes away. A very long name pushes the fingerprint off the visible prompt,
    // which is the de-emphasis D-8 forbids — achieved with no UI change at all.
    [Fact]
    public void ANameLongerThanTheBoundIsRefusedAndOneAtTheBoundIsNot()
    {
        Assert.True(DisplayName.TryParse(new string('B', DisplayName.MaxLength), out _));
        Assert.False(DisplayName.TryParse(new string('B', DisplayName.MaxLength + 1), out _));
    }

    // Surrounding whitespace is repaired because " Bob " means Bob and the difference is invisible
    // on screen. Nothing else is repaired — see ANameCarryingAControlCharacterIsRefused.
    [Fact]
    public void SurroundingWhitespaceIsTrimmedRatherThanRefused()
    {
        Assert.True(DisplayName.TryParse("  Bob  ", out var name));
        Assert.Equal("Bob", name.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyNameIsNotAName(string? candidate)
    {
        Assert.False(DisplayName.TryParse(candidate, out _));
    }

    // Fails if: an absent name renders as a blank. A gap where a name should be reads as a
    // rendering fault to a DM who is being asked to make a security decision, and a DM who thinks
    // the prompt is broken is a DM who stops reading it — including the fingerprint beside it.
    [Fact]
    public void AnAbsentNameStillRendersAsSomething()
    {
        Assert.False(DisplayName.None.WasStated);
        Assert.NotEmpty(DisplayName.None.Value);
        Assert.Equal(DisplayName.Unstated, DisplayName.None.Value);
    }

    // OrNone is the wire's entry point: a refusal must not become a dropped request, because the
    // person behind a bad name is still waiting to be admitted.
    [Fact]
    public void OrNoneFallsBackInsteadOfThrowing()
    {
        Assert.Equal(DisplayName.None, DisplayName.OrNone("Bob\nnot really"));
        Assert.Equal(DisplayName.None, DisplayName.OrNone(null));
        Assert.Equal("Bob", DisplayName.OrNone("Bob").Value);
    }

    // Fails if: two identical names stop comparing equal, or differing ones start. A-1.2d depends
    // on callers NOT using this to tell requesters apart, and equality is what makes that testable.
    [Fact]
    public void TwoParticipantsMayHoldTheSameName()
    {
        Assert.Equal(DisplayName.OrNone("Bob"), DisplayName.OrNone("Bob"));
        Assert.NotEqual(DisplayName.OrNone("Bob"), DisplayName.OrNone("Rob"));
    }
}
