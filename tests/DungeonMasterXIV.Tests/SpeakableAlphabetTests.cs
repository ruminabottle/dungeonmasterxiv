using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The grouping half of the shared convention. The alphabet's own contents — no vowels, no
/// confusable characters, 24 distinct symbols — are already asserted by <c>SessionCodeTests</c>
/// against <c>SessionCode.Alphabet</c>, which is now defined as this type's value; repeating those
/// assertions here would test the same constant twice rather than test anything more.
/// </summary>
public class SpeakableAlphabetTests
{
    // A six-character session code reads as two groups of three. Guards the display format against
    // the grouping being rewritten for the fingerprint's benefit.
    [Fact]
    public void SixCharactersGroupAsThreeAndThree()
    {
        Assert.Equal("BKD-7RM", SpeakableAlphabet.Group("BKD7RM"));
    }

    // An eleven-character fingerprint reads as three-three-three-two. The trailing partial group is
    // the point: R-1.3a's eleven does not divide by three, and an implementation that dropped or
    // padded the remainder would either lose a character's worth of entropy or invent one.
    [Fact]
    public void ElevenCharactersGroupAsThreeThreeThreeTwo()
    {
        Assert.Equal("BCD-FGH-JKM-NP", SpeakableAlphabet.Group("BCDFGHJKMNP"));
    }

    [Fact]
    public void GroupingKeepsEveryCharacterItWasGiven()
    {
        const string Rendered = "BCDFGHJKMNP";

        Assert.Equal(Rendered, SpeakableAlphabet.Group(Rendered).Replace("-", string.Empty));
    }

    // A group size that divides the length exactly must not emit a trailing hyphen.
    [Fact]
    public void AnExactMultipleOfTheGroupSizeHasNoTrailingSeparator()
    {
        Assert.Equal("BCD-FGH", SpeakableAlphabet.Group("BCDFGH"));
    }
}
