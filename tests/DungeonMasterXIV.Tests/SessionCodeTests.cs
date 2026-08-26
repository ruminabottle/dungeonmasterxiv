using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class SessionCodeTests
{
    // Fails if: the alphabet gains a vowel, or loses one of the confusable exclusions. R-1.2a
    // calls the exclusions the decision, so they are asserted rather than left to the comment.
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
    public void TheAlphabetExcludesEveryCharacterRule12aExcludes(char excluded)
    {
        Assert.DoesNotContain(excluded, SessionCode.Alphabet);
    }

    // Fails if: a character is duplicated or dropped — either changes the keyspace R-1.2a states.
    [Fact]
    public void TheAlphabetIsTwentyFourDistinctCharacters()
    {
        Assert.Equal(24, SessionCode.Alphabet.Length);
        Assert.Equal(24, SessionCode.Alphabet.Distinct().Count());
    }

    // Fails if: the generator returns one character, or five, or seven. This is the case the
    // standards name explicitly — a non-empty check would pass over a one-character generator.
    [Fact]
    public void AGeneratedCodeIsExactlySixCharacters()
    {
        Assert.Equal(6, SessionCodeGenerator.Next().Value.Length);
    }

    // Fails if: the generator draws from a wider alphabet than the one R-1.2a fixed.
    [Fact]
    public void EveryCharacterOfEveryGeneratedCodeIsInTheAlphabet()
    {
        for (var i = 0; i < 500; i++)
        {
            foreach (var character in SessionCodeGenerator.Next().Value)
            {
                Assert.Contains(character, SessionCode.Alphabet);
            }
        }
    }

    // Fails if: the generator returns a constant. 500 draws from a ~191 million keyspace collapsing
    // to one value does not happen by chance; it happens when the randomness is not wired up.
    [Fact]
    public void TheGeneratorDoesNotReturnTheSameCodeEveryTime()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => SessionCodeGenerator.Next().Value).ToHashSet();

        Assert.True(codes.Count > 1, $"Generator produced {codes.Count} distinct code(s) in 500 draws.");
    }

    // Fails if: ToDisplayString returns the raw six characters, or groups them 2-4, or drops the
    // hyphen. R-1.2a fixes the presentation as two groups of three.
    [Fact]
    public void ACodeIsDisplayedAsTwoGroupsOfThree()
    {
        var code = SessionCode.FromValid("BKD7RM");

        Assert.Equal("BKD-7RM", code.ToDisplayString());
    }

    // Fails if: parsing becomes case-sensitive or hyphen-intolerant. R-1.2 requires a code that can
    // be read aloud and typed without care, which is what these three spellings represent.
    [Theory]
    [InlineData("BKD-7RM")]
    [InlineData("BKD7RM")]
    [InlineData("bkd-7rm")]
    [InlineData("  BKD-7RM  ")]
    public void ACodeParsesHoweverAHumanReasonablyTypesIt(string typed)
    {
        Assert.True(SessionCode.TryParse(typed, out var code));
        Assert.Equal("BKD7RM", code.Value);
    }

    // Fails if: the length check is removed.
    [Theory]
    [InlineData("BKD7R")]
    [InlineData("BKD7RMX")]
    [InlineData("")]
    public void ACodeOfTheWrongLengthIsRejected(string typed)
    {
        Assert.False(SessionCode.TryParse(typed, out _));
    }

    // Fails if: the alphabet check is removed. Each of these is six characters, so only the
    // membership test can reject them.
    [Theory]
    [InlineData("AEIOUY")]
    [InlineData("LLLLLL")]
    [InlineData("000000")]
    [InlineData("BKD7R!")]
    public void ACodeContainingExcludedCharactersIsRejected(string typed)
    {
        Assert.False(SessionCode.TryParse(typed, out _));
    }

    // Fails if: display and parse stop agreeing — the code a DM reads aloud must be the code the
    // player types back.
    [Fact]
    public void ADisplayedCodeParsesBackToTheSameCode()
    {
        var original = SessionCodeGenerator.Next();

        Assert.True(SessionCode.TryParse(original.ToDisplayString(), out var reparsed));
        Assert.Equal(original, reparsed);
    }
}
