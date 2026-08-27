using System;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

public class ReleaseInputsTests
{
    private const string Repo = "https://github.com/ruminabottle/dungeonmasterxiv";

    // The version is DERIVED from the tag rather than written beside it. Both fixtures here used to
    // pair "v0.1.0" with 0.0.0.1 -- a disagreeing pair, and BUG-14 is exactly that pair being
    // accepted, so no test in this file could construct the defect it needed to see.
    private static ReleaseInputs Valid(string tag = "v0.1.0", int apiLevel = 13, string? assetName = null) =>
        new(tag, TaggedVersion.Of(tag), apiLevel, Repo, Assets.Any(assetName ?? Assets.PackagerName));

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
        // Built explicitly rather than through Valid(), which derives its version FROM the tag and
        // would throw while constructing the fixture -- the assertion would then pass without
        // Validate() ever running.
        var inputs = new ReleaseInputs(tag, new Version(0, 1, 0, 0), 13, Repo, Assets.Any());

        Assert.Throws<ArgumentException>(inputs.Validate);
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
        // The tag and the version agree, so this reaches the URL check. Paired with a version the
        // tag does not name, it would still throw and still pass -- for the wrong reason.
        var inputs = new ReleaseInputs("v1.0.0", TaggedVersion.Of("v1.0.0"), 13, repoUrl, Assets.Any());

        Assert.Throws<ArgumentException>(inputs.Validate);
    }

    // A-7.3. The link is built from the tag, so it cannot accidentally become a branch URL.
    [Fact]
    public void TheDownloadLinkPointsAtTheTaggedReleaseAsset()
    {
        Assert.Equal(
            $"{Repo}/releases/download/v0.1.0/{Assets.PackagerName}",
            Valid().DownloadLink);
    }

    // C19. The file name in the link is READ OFF THE ASSET, so the link moves when the file does.
    // It used to be the constant "DungeonMasterXIV.zip", a name DalamudPackager has never written --
    // it writes latest.zip -- which made every link 404 while the manifest, the release and the
    // plugin were all fine. Two names rather than one: against the old constant the first case fails
    // and the second passes, and a single case named latest.zip could be a constant that happened to
    // be right rather than a name that was derived.
    [Theory]
    [InlineData("latest.zip")]
    [InlineData("DungeonMasterXIV-v0.1.0.zip")]
    public void TheLinkNamesTheFileTheAssetActuallyIs(string assetName)
    {
        Assert.EndsWith($"/{assetName}", Valid(assetName: assetName).DownloadLink, StringComparison.Ordinal);
    }
}
