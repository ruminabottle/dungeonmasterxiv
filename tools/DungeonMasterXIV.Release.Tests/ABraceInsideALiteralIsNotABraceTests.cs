using DungeonMasterXIV.Sizes;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The span reader must not count braces that live inside comments or literals.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ruled procedure already answers this</b> — a class span runs to <i>its closing brace</i>,
/// and a <c>'}'</c> in a char literal is not one. So this is a reader that misread the rule rather
/// than a question about what the rule counts, which is why it was fixed rather than ruled on.
/// </para>
/// <para>
/// <b>Both arms are real, and only one of them is honest.</b> An unmatched <c>'{'</c> makes the scan
/// run off the end and the type is REFUSED — visible in the census. An unmatched <c>'}'</c> closes
/// the type EARLY and reports a falsely short span. <b>That arm was live in this tree:</b>
/// <c>JoinOverASocketTests</c> was reported as <b>155 lines when it is 172</b>, closed seventeen
/// lines early by the <c>"\"}"</c> in a JSON padding fixture.
/// </para>
/// <para>
/// <b>Every case here is drawn from a construction that actually occurs in this repository</b>,
/// not invented to be caught. A control that fires only on the spelling its author happened to pick
/// is not a control — the guard in DMXENG-39 nearly shipped with exactly that flaw, blind to the
/// target-typed <c>new()</c> that four factories use.
/// </para>
/// </remarks>
public class ABraceInsideALiteralIsNotABraceTests
{
    // From ShippedCopyCorpus and FactoryOverloads: a brace matcher naturally switches on braces.
    // ClassSpanReader's own case '{' / case '}' cancel out, so the tool could measure itself only
    // by luck -- an unbalanced one would have refused the reader that does the measuring.
    [Theory]
    [InlineData("case '{':")]
    [InlineData("case '}':")]
    [InlineData("above.EndsWith('{')")]
    public void ABraceInACharLiteralIsBlanked(string line)
    {
        var inBlockComment = false;

        Assert.DoesNotContain('{', CodeOnly.Of(line, ref inBlockComment));
        Assert.DoesNotContain('}', CodeOnly.Of(line, ref inBlockComment));
    }

    // The five refusals nobody had attributed. All JSON fixtures in the campaign and wire tests --
    // and string literals, not char literals, were the majority cause. An estimate drawn from char
    // literals alone undercounted the blast radius by a factor of three.
    [Theory]
    [InlineData("[InlineData(\"{ not json\")]")]
    [InlineData("[InlineData(\"{\\\"Type\\\":\")]")]
    [InlineData("var x = $\"{{\\\"Version\\\":1}}\";")]
    public void ABraceInAStringLiteralIsBlanked(string line)
    {
        var inBlockComment = false;

        Assert.DoesNotContain('{', CodeOnly.Of(line, ref inBlockComment));
        Assert.DoesNotContain('}', CodeOnly.Of(line, ref inBlockComment));
    }

    // THE LINE THAT WAS ACTUALLY LYING. JoinOverASocketTests:161 ends a JSON document with an
    // escaped quote followed by a close brace. The escape is what makes it hard: a reader that
    // treats \" as ending the literal sees the '}' as code.
    [Fact]
    public void TheEscapedQuoteCaseThatShortenedARealClassIsHandled()
    {
        var inBlockComment = false;
        var line = "var padded = json[..^1] + \",\\\"Padding\\\":\\\"\" + new string('x', 20) + \"\\\"}\";";

        Assert.DoesNotContain('}', CodeOnly.Of(line, ref inBlockComment));
    }

    // ESCAPE HANDLING, PINNED BECAUSE MUTATION FOUND IT UNPINNED. Changing the escape to consume
    // ONE character instead of two left all thirteen tests green -- including the one named for the
    // real escaped-quote line, which stays blanked by luck once the re-parse re-opens a literal over
    // the rest of it.
    //
    // This case does not: after a mis-parsed \" the closing quote is taken as the literal's end and
    // the following brace is read as CODE. Verified by running the mutation against it directly.
    //
    // The lesson is the one from DMXENG-39 an hour ago -- a control that fires on the input its
    // author happened to pick is not a control, and the fix is to find the input that DISCRIMINATES
    // rather than one that merely passes.
    [Fact]
    public void AnEscapedQuoteDoesNotEndTheLiteral()
    {
        var inBlockComment = false;

        Assert.DoesNotContain('}', CodeOnly.Of("var s = \"a\\\"} b\";", ref inBlockComment));
    }

    // A VERBATIM STRING'S "" ESCAPE IS NOT OBSERVABLE FOR BRACE COUNTING, AND THIS IS REPORTED
    // RATHER THAN COVERED. Deleting that branch leaves all fourteen tests green, and unlike the
    // escape case above I could not find an input that discriminates -- because I do not think one
    // exists on a single well-formed line.
    //
    // The reason: mis-parsing "" splits ONE literal into TWO that between them cover the same
    // characters. Every brace inside the correct literal is still inside one of the two, so the
    // classification of every brace is unchanged. The branch is defence for a case the counting
    // cannot see, not coverage anybody is missing.
    //
    // Left in place: it is correct, it costs nothing, and removing a right thing because no test
    // can watch it is how a reader stops being able to read its own inputs. Said out loud because
    // an untested branch that nobody has explained reads exactly like an oversight.
    [Fact]
    public void ABraceInAVerbatimStringIsBlanked()
    {
        var inBlockComment = false;

        Assert.DoesNotContain('}', CodeOnly.Of("var x = @\"a } b\";", ref inBlockComment));
    }

    // The previous reader blanked only lines that STARTED with a comment, so a trailing one counted.
    [Fact]
    public void ABraceInATrailingLineCommentIsBlanked()
    {
        var inBlockComment = false;

        Assert.DoesNotContain('}', CodeOnly.Of("var x = 1; // closes with }", ref inBlockComment));
    }

    [Fact]
    public void ABraceInABlockCommentIsBlanked()
    {
        var inBlockComment = false;

        Assert.DoesNotContain('}', CodeOnly.Of("var x = 1; /* } */ var y = 2;", ref inBlockComment));
        Assert.False(inBlockComment, "a block comment closed on the same line must not leak state");
    }

    // The one construct that needs state carried between lines. Without the ref parameter the middle
    // of a multi-line comment reads as code.
    [Fact]
    public void ABlockCommentSpanningLinesKeepsItsStateAcrossThem()
    {
        var inBlockComment = false;

        CodeOnly.Of("/* opens here", ref inBlockComment);
        Assert.True(inBlockComment);

        Assert.DoesNotContain('}', CodeOnly.Of("still inside }", ref inBlockComment));
        Assert.True(inBlockComment, "the comment has not closed yet");

        CodeOnly.Of("closes */", ref inBlockComment);
        Assert.False(inBlockComment);
    }

    // THE NEGATIVE HALF, AND WITHOUT IT EVERY TEST ABOVE PASSES AGAINST A FUNCTION THAT RETURNS
    // BLANKS. Real braces must survive, or the reader finds no types at all and every span is
    // refused -- which would read as "nothing to measure" rather than as a broken tool.
    [Fact]
    public void ABraceThatIsCodeSurvives()
    {
        var inBlockComment = false;
        var code = CodeOnly.Of("public void M() { Act(); }", ref inBlockComment);

        Assert.Contains('{', code);
        Assert.Contains('}', code);
    }

    // Blanking rather than deleting: nothing downstream uses column positions today, and a reader
    // that quietly shortens lines is a trap for whatever does next.
    [Fact]
    public void TheLineKeepsItsLength()
    {
        var inBlockComment = false;
        const string Line = "var x = \"a } b\"; // and } here";

        Assert.Equal(Line.Length, CodeOnly.Of(Line, ref inBlockComment).Length);
    }
}
