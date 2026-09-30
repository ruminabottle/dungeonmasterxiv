using Xunit;

namespace DungeonMasterXIV.CommentPass.Tests;

public class DeadPatternsTests
{
    private const string D = "-";

    public static TheoryData<string> Dead => new()
    {
        "BUG" + D + "87", "DMXENG" + D + "105", "SQ" + D + "84", "PRD" + D + "1", "(E" + D + "3)", "(T" + D + "37)",
        "The Spec" + " Owner ruled", "the Deployment" + " Manager", "the deployment" + " manager",
        "Product" + " Owner", "Engineering" + " Lead", "found by the code" + " reviewer",
        "the Code" + " Reviewer", "a b" + "reakfix engineer", "ruled by the " + "HUMAN",
        "in ." + "claude/team/X.md", "engineering" + D + "standards.md", "product" + D + "directives",
        "the " + "brief.md", "found by qa" + D + "3", "found by QA" + D + "3",
    };

    public static TheoryData<string> Alive => new()
    {
        "R" + D + "1.3h", "A" + D + "2.40", "D" + D + "11", "1E" + D + "10", "SHA" + D + "256", "UTF" + D + "8",
        "#89", "the human reading the roster", "a ticket", "QA",
    };

    public static TheoryData<string> Review => new()
    {
        "Decision 10", "QA", "since #120", "at 2719162", "the bug above", "that ticket", "this ruling",
        "dmx-bug17", "the human",
    };

    [Theory, MemberData(nameof(Dead))]
    public void TheGuardFindsEachDeadForm(string text) =>
        Assert.NotEmpty(DeadPatterns.Find(text, DeadPatterns.Guard));

    [Theory, MemberData(nameof(Alive))]
    public void TheGuardLeavesLiveFormsAlone(string text) =>
        Assert.Empty(DeadPatterns.Find(text, DeadPatterns.Guard));

    [Theory, MemberData(nameof(Review))]
    public void TheReviewSetFindsEachQuestionableForm(string text) =>
        Assert.NotEmpty(DeadPatterns.Find(text, DeadPatterns.Review));

    [Fact]
    public void FindReportsOneBasedLineNumbers()
    {
        var hits = DeadPatterns.Find("clean\nclean\nsee BUG" + D + "1 here", DeadPatterns.Guard);
        Assert.Equal(3, Assert.Single(hits).Line);
    }

    [Fact]
    public void ThePatternSourceDoesNotMatchItself()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "DungeonMasterXIV.CommentPass", "DeadPatterns.cs"));
        Assert.Empty(DeadPatterns.Find(source, DeadPatterns.Guard));
    }

    internal static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DungeonMasterXIV.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
