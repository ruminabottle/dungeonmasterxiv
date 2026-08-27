using System;
using System.IO;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// C19. The asset is identified by the file on disk, and the file is checked against the assembly
/// the manifest describes (A-7.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two separate defects, and the second is the worse one.</b> The name used to be a constant
/// reading <c>DungeonMasterXIV.zip</c> while DalamudPackager writes <c>latest.zip</c>, so a tester's
/// install 404'd with nothing on our side looking wrong. Deriving the name fixes that and leaves the
/// other one standing: <b>every build writes the same name</b>, so a correctly-named zip can still be
/// the wrong build. That installs and then misbehaves, which is worse than a dead link because it
/// looks like it worked.
/// </para>
/// <para>
/// <b>Neither name nor version can separate those builds.</b> Five were on this machine at once,
/// 61KB to 119KB across two days, all named <c>latest.zip</c> and all carrying <c>0.0.0.1</c> —
/// measured, not assumed. A version-only check passes on every one of them. Only the bytes differ,
/// which is what <see cref="ReleaseAsset.MustMatchTheAssembly"/> compares and what makes A-7.2's
/// "the built assembly <i>it links to</i>" a claim that can come out negative.
/// </para>
/// </remarks>
public class ReleaseAssetTests
{
    private const string ABuild = "the bytes of one build";
    private const string ADifferentBuild = "the bytes of another build";

    // The name is taken off the file, never assumed. Two names, because a single case using the
    // packager's own name would also pass against a constant that happened to say latest.zip.
    [Theory]
    [InlineData("latest.zip")]
    [InlineData("DungeonMasterXIV-v0.1.0.zip")]
    public void TheNameIsTheNameOfTheFileOnDisk(string name)
    {
        Assert.Equal(name, ReleaseAsset.At(Assets.Zip(name, (Assets.PluginAssembly, ABuild))).Name);
    }

    // The failing input the whole check exists for: a path that resolves to nothing. A manifest built
    // from it would be well-formed and point at a file that was never uploaded, so this refuses
    // rather than deriving a name from a path -- and it names the path, because "not found" without
    // one is the message that gets guessed at instead of read.
    [Fact]
    public void APathWithNothingAtTheEndOfItIsRefusedByPath()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-directory", "latest.zip");

        var failure = Assert.Throws<FileNotFoundException>(() => ReleaseAsset.At(missing));

        Assert.Contains(missing, failure.Message, StringComparison.Ordinal);
    }

    // There is no default asset name and there must not be one: a default is the old constant with a
    // longer fuse. An omitted --asset therefore has to stop the run, not fall back to a guess.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankPathIsRefusedRatherThanDefaulted(string path)
    {
        Assert.Throws<ArgumentException>(() => ReleaseAsset.At(path));
    }

    // A-7.2, positive side.
    [Fact]
    public void AZipCarryingTheAssemblyTheManifestDescribesIsAccepted()
    {
        var asset = ReleaseAsset.At(Assets.Zip(Assets.PackagerName, (Assets.PluginAssembly, ABuild)));

        asset.MustMatchTheAssembly(Assets.File(Assets.PluginAssembly, ABuild));
    }

    // A-7.2, and the case the requirement is FOR. Same file name, same version, different build --
    // the stale-zip input that a name check and a version check both wave through. If this stops
    // failing, A-7.2 is no longer being proved by anything.
    [Fact]
    public void AZipFromADifferentBuildIsRefused()
    {
        var asset = ReleaseAsset.At(Assets.Zip(Assets.PackagerName, (Assets.PluginAssembly, ADifferentBuild)));

        var failure = Assert.Throws<InvalidOperationException>(
            () => asset.MustMatchTheAssembly(Assets.File(Assets.PluginAssembly, ABuild)));

        Assert.Contains("different build", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    // A zip with no plugin in it is not a release zip, and saying "does not match" would send someone
    // looking for a stale build instead of a wrong path.
    [Fact]
    public void AZipThatIsNotAPluginReleaseIsRefusedAsSuch()
    {
        var asset = ReleaseAsset.At(Assets.Zip(Assets.PackagerName, ("readme.txt", "not a plugin")));

        var failure = Assert.Throws<InvalidOperationException>(
            () => asset.MustMatchTheAssembly(Assets.File(Assets.PluginAssembly, ABuild)));

        Assert.Contains(Assets.PluginAssembly, failure.Message, StringComparison.Ordinal);
    }

    // --asset pointed at the DLL rather than the zip beside it is an easy slip: sibling directories,
    // one path segment apart. The raw failure is "End of Central Directory record could not be
    // found", which names neither the file nor the mistake, so it is replaced by one that does.
    [Fact]
    public void SomethingThatIsNotAZipIsRefusedByPath()
    {
        var notAZip = Assets.File(Assets.PackagerName, "not a zip");

        var failure = Assert.Throws<InvalidOperationException>(
            () => ReleaseAsset.At(notAZip).MustMatchTheAssembly(Assets.File(Assets.PluginAssembly, ABuild)));

        Assert.Contains(notAZip, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Central Directory", failure.Message, StringComparison.Ordinal);
    }

    // The assembly side of the comparison gets the same treatment as the asset side: absent means
    // stop, not "nothing to compare against, so it matches".
    [Fact]
    public void AnAssemblyThatIsNotThereIsRefusedRatherThanTreatedAsAMatch()
    {
        var asset = ReleaseAsset.At(Assets.Zip(Assets.PackagerName, (Assets.PluginAssembly, ABuild)));

        Assert.ThrowsAny<Exception>(
            () => asset.MustMatchTheAssembly(Path.Combine(Path.GetTempPath(), "no-such", "DungeonMasterXIV.dll")));
    }
}
