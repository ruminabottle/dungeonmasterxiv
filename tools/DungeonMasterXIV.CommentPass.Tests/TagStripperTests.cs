using Xunit;

namespace DungeonMasterXIV.CommentPass.Tests;

public class TagStripperTests
{
    private const string D = "-";
    private static readonly string Bug87 = "BUG" + D + "87";
    private static readonly string Bug115 = "BUG" + D + "115";
    private static readonly string Eng70 = "DMXENG" + D + "70";
    private static readonly string T37 = "T" + D + "37";

    [Fact]
    public void APureTagBeforePunctuationGoesWithItsSpace() =>
        Assert.Equal("TWO NULLS APART.</b>", TagStripper.Strip($"TWO NULLS APART ({Bug87}).</b>"));

    [Fact]
    public void APureTagMidSentenceLeavesOneSpace() =>
        Assert.Equal("ruled here and kept", TagStripper.Strip($"ruled here ({Eng70}) and kept"));

    [Fact]
    public void APureListOfTagsGoesWhole() =>
        Assert.Equal("measured.", TagStripper.Strip($"measured ({Bug87}, {Eng70})."));

    [Fact]
    public void ATrailingTagInACitationListGoes() =>
        Assert.Equal("(R" + D + "1.3h)", TagStripper.Strip($"(R{D}1.3h, {Bug115})"));

    [Fact]
    public void ALeadingTagInACitationListGoes() =>
        Assert.Equal("(R" + D + "1.5)", TagStripper.Strip($"({T37}, R{D}1.5)"));

    [Fact]
    public void TagsOnBothEndsOfAListGo() =>
        Assert.Equal("(A" + D + "1.2z)", TagStripper.Strip($"({Bug87}, A{D}1.2z, {Bug115})"));

    [Fact]
    public void ATagThatIsPartOfASentenceIsLeftForJudgment()
    {
        var text = $"which is the collision {Bug87} was";
        Assert.Equal(text, TagStripper.Strip(text));
    }

    [Fact]
    public void AnAmendedTagIsLeftForJudgment()
    {
        var text = $"(amended {Eng70} and later)";
        Assert.Equal(text, TagStripper.Strip(text));
    }

    [Fact]
    public void ADocCommentLineStartingWithATagKeepsItsMarker() =>
        Assert.Equal("/// the rule", TagStripper.Strip($"/// ({Bug87}) the rule"));

    [Fact]
    public void ATagRightAfterTheMarkerAndBeforePunctuationIsLeftForJudgment()
    {
        var text = $"/// ({Bug87}).</b>";
        Assert.Equal(text, TagStripper.Strip(text));
    }

    [Fact]
    public void SpecIdsAreNeverTouched()
    {
        var text = $"(R{D}1.3h) and (A{D}2.40, D{D}11)";
        Assert.Equal(text, TagStripper.Strip(text));
    }
}
