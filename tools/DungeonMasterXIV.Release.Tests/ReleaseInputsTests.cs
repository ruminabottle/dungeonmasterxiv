using System;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

public class ReleaseInputsTests
{
    private static ReleaseInputs Valid(string tag = "v0.1.0", int apiLevel = 13) =>
        new(tag, new Version(0, 0, 0, 1), apiLevel, "https://github.com/ruminabottle/dungeonmasterxiv");

    [Fact]
    public void CompleteInputsValidate()
    {
        Valid().Validate();
    }

    // Nothing is defaulted, and these are the reasons. A default here would be a value this tool
    // invented and handed to Dalamud as fact, and each failure it produces is silent.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingTagIsRefused(string tag)
    {
        Assert.Throws<ArgumentException>(() => Valid(tag: tag).Validate());
    }

    // A wrong API level makes Dalamud never offer the plugin, with nothing written anywhere we would
    // see. Since R-7.3a the value is copied from the built manifest rather than typed, so an unusable
    // one means the BUILD did not produce what we expected -- and the message has to say that, not
    // that a number is missing. "A number is missing" reads as a queue somebody clears by guessing,
    // which is the behaviour deriving the value exists to remove.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnApiLevelTheBuildDidNotProduceIsRefused(int apiLevel)
    {
        var failure = Assert.Throws<ArgumentException>(() => Valid(apiLevel: apiLevel).Validate());

        Assert.Contains("built plugin manifest", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not something to supply by hand", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("github.com/ruminabottle/dungeonmasterxiv")]
    [InlineData("http://github.com/ruminabottle/dungeonmasterxiv")]
    [InlineData("not a url")]
    public void ARepositoryUrlThatIsNotAbsoluteHttpsIsRefused(string repoUrl)
    {
        var inputs = new ReleaseInputs("v0.1.0", new Version(1, 0), 13, repoUrl);

        Assert.Throws<ArgumentException>(inputs.Validate);
    }

    // A-7.3. The link is built from the tag, so it cannot accidentally become a branch URL.
    [Fact]
    public void TheDownloadLinkPointsAtTheTaggedReleaseAsset()
    {
        Assert.Equal(
            "https://github.com/ruminabottle/dungeonmasterxiv/releases/download/v0.1.0/DungeonMasterXIV.zip",
            Valid().DownloadLink);
    }
}
