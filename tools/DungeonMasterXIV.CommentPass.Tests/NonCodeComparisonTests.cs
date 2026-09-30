using Xunit;

namespace DungeonMasterXIV.CommentPass.Tests;

public class NonCodeComparisonTests
{
    private const string Project = """
        <Project>
          <!--
            Old words (tag).
          -->
          <PropertyGroup>
            <OutputType>Exe</OutputType>
          </PropertyGroup>
        </Project>
        """;

    [Fact]
    public void AProjectCommentChangeIsNotADifference() =>
        Assert.Null(NonCodeComparison.Difference("x/A.csproj", Project, Project.Replace("Old words (tag).", "New.")));

    [Fact]
    public void AProjectCommentRemovedWholeIsNotADifference() =>
        Assert.Null(NonCodeComparison.Difference(
            "Directory.Build.targets", Project, Project.Replace("  <!--\n    Old words (tag).\n  -->\n", "")));

    [Fact]
    public void AProjectPropertyChangeIsADifference() =>
        Assert.NotNull(NonCodeComparison.Difference("x/A.csproj", Project, Project.Replace("Exe", "Library")));

    [Theory]
    [InlineData("deploy/Dockerfile")]
    [InlineData(".gitignore")]
    [InlineData(".dockerignore")]
    [InlineData("tools/DungeonMasterXIV.Release.Tests/size-gate-baseline.txt")]
    public void AHashLineChangeIsNotADifference(string path) =>
        Assert.Null(NonCodeComparison.Difference(path, "# old (tag)\nFROM x\n", "# new\nFROM x\n"));

    [Theory]
    [InlineData("deploy/Dockerfile")]
    [InlineData(".gitignore")]
    public void AnInstructionChangeIsADifference(string path) =>
        Assert.NotNull(NonCodeComparison.Difference(path, "# c\nFROM x\n", "# c\nFROM y\n"));

    [Fact]
    public void TheClockFixtureHeaderMayChangeButItsFormsMayNot()
    {
        const string before = "Title line.\n\nOld header (tag).\nMore header.\n\nDateTime.Now\nStopwatch\n";
        var path = "tests/DungeonMasterXIV.Tests/ClockFormsFixture.txt";
        Assert.Null(NonCodeComparison.Difference(path, before, before.Replace("Old header (tag).", "New header.")));
        Assert.NotNull(NonCodeComparison.Difference(path, before, before.Replace("Stopwatch", "Stopwatc")));
    }

    [Fact]
    public void AFileTypeWithNoRuleIsADifference() =>
        Assert.NotNull(NonCodeComparison.Difference("README.md", "a", "b"));
}
