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
