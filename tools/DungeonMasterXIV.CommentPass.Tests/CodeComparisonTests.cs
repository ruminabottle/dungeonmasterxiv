using Xunit;

namespace DungeonMasterXIV.CommentPass.Tests;

public class CodeComparisonTests
{
    private const string Before = """
        /// <summary>Old words (tag).</summary>
        public class C
        {
            // an old comment
            public string M() => "old message";
        }
        """;

    [Fact]
    public void ACommentOnlyChangeIsNotACodeChange()
    {
        var after = Before.Replace("Old words (tag).", "New words.").Replace("an old comment", "new");
        var result = CodeComparison.Compare(Before, after);
        Assert.False(result.CodeChanged, result.CodeDifference);
        Assert.Empty(result.Literals);
    }

    [Fact]
    public void ALiteralChangeIsReportedWithItsLine()
    {
        var result = CodeComparison.Compare(Before, Before.Replace("old message", "new message"));
        Assert.False(result.CodeChanged, result.CodeDifference);
        var change = Assert.Single(result.Literals);
        Assert.Equal(5, change.Line);
        Assert.Equal("\"old message\"", change.Before);
        Assert.Equal("\"new message\"", change.After);
    }

    [Fact]
    public void AnInterpolatedTextChangeIsALiteralChange()
    {
        var before = "class C { string M(int n) => $\"old {n} words\"; }";
        var result = CodeComparison.Compare(before, before.Replace("old ", "new "));
        Assert.False(result.CodeChanged, result.CodeDifference);
        Assert.Single(result.Literals);
    }

    [Fact]
    public void AnIdentifierChangeIsACodeChange() =>
        Assert.True(CodeComparison.Compare(Before, Before.Replace("M()", "N()")).CodeChanged);

    [Fact]
    public void ARemovedTokenIsACodeChange() =>
        Assert.True(CodeComparison.Compare(Before, Before.Replace("public class", "class")).CodeChanged);

    [Fact]
    public void ADroppedConcatenationPieceIsACodeChange()
    {
        var before = "class C { string M() => \"a\" + \"b\"; }";
        Assert.True(CodeComparison.Compare(before, "class C { string M() => \"a\"; }").CodeChanged);
    }

    [Fact]
    public void APreprocessorDirectiveChangeIsACodeChange()
    {
        var before = "#if DEBUG\nclass C { }\n#endif\n";
        Assert.True(CodeComparison.Compare(before, before.Replace("DEBUG", "RELEASE")).CodeChanged);
    }

    [Fact]
    public void DisabledTextIsCode()
    {
        var before = "#if NEVER\nclass Old { }\n#endif\nclass C { }\n";
        Assert.True(CodeComparison.Compare(before, before.Replace("Old", "New")).CodeChanged);
    }
}
