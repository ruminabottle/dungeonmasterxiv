using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The shared speakable convention: how it groups, and what it contains.
/// </summary>
/// <remarks>
/// <para>
/// This file previously covered grouping only, and said so deliberately — the alphabet's contents
/// were asserted by <c>SessionCodeTests</c> against <c>SessionCode.Alphabet</c>, and since that is a
/// one-line alias for this constant, repeating them here looked like testing the same value twice.
/// </para>
/// <para>
/// <b>That reasoning was correct and has stopped being correct, which is the interesting part.</b>
/// It rested on the alias holding, and the single reason anyone ever edits these types — session
/// codes and key fingerprints needing to diverge — is the reason it would stop holding. At that
/// moment the consumer's tests keep passing against the consumer's own new alphabet, and the shared
/// type loses every assertion about its contents without one test going red. A conclusion outliving
/// the premise it rested on, with nothing in the code recording which premise it was.
/// </para>
/// </remarks>
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

    /// <summary>
    /// The alphabet's contents are asserted against the shared type itself, not through a consumer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SessionCodeTests"/> asserts the same properties against <c>SessionCode.Alphabet</c>,
    /// which today is a one-line alias for this constant — so while the alias holds, these two
    /// assertions cannot fail independently of each other.
    /// </para>
    /// <para>
    /// <b>They are here for the moment the alias stops holding.</b> The one reason anybody ever
    /// touches this is session codes and key fingerprints needing to diverge. At that point
    /// <c>SessionCode</c> takes its own alphabet back, its own tests keep passing against it, and the
    /// shared type silently loses every assertion about its contents — while the grouping tests above
    /// keep passing, because they never look at what the characters are. The fingerprint would then
    /// be the sole consumer of an entirely untested alphabet.
    /// </para>
    /// <para>
    /// Both sets are kept rather than one moved. After a divergence each type needs its own alphabet
    /// asserted, and deleting the consumer's copy would trade one coverage hole for another.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheSharedAlphabetIsTwentyFourDistinctCharacters()
    {
        Assert.Equal(24, SpeakableAlphabet.Characters.Length);
        Assert.Equal(24, SpeakableAlphabet.Characters.Distinct().Count());
        Assert.Equal(SpeakableAlphabet.Characters.Length, SpeakableAlphabet.Length);
    }

    /// <summary>
    /// Every exclusion R-1.2a names is absent from the shared alphabet. Asserted here for the same
    /// reason as the count: the exclusions are the decision, and a consumer's alias is not where a
    /// decision should be guarded.
    /// </summary>
    [Theory]
    [InlineData('A')]
    [InlineData('E')]
    [InlineData('I')]
    [InlineData('O')]
    [InlineData('U')]
    [InlineData('L')]
    [InlineData('S')]
    [InlineData('Z')]
    [InlineData('Q')]
    [InlineData('0')]
    [InlineData('1')]
    [InlineData('5')]
    public void TheSharedAlphabetExcludesEveryCharacterRule12aExcludes(char excluded)
    {
        Assert.DoesNotContain(excluded, SpeakableAlphabet.Characters);
    }
}
