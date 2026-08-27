using System;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// A-7.2a and A-7.2b: the advertised version has exactly one author, and it is the git tag.
/// </summary>
/// <remarks>
/// <para>
/// <b>These replace A-7.2, which could not fail against the defect it existed to catch.</b> A-7.2
/// asked that the manifest match the built assembly. In BUG-14's reproduction it did match: both
/// read <c>0.0.0.1</c>, the criterion passed, and four different tags against one unchanged build
/// all advertised that same version. Dalamud does not reject a repeated version — it never offers
/// the build, with nothing logged on our side, so the symptom is a tester who goes quiet.
/// </para>
/// <para>
/// <b>The first test evaluates the real csproj rather than a copy of its logic.</b> The derivation
/// under test lives in MSBuild, so asserting it in C# would test a reimplementation and pass while
/// the build did something else. <c>-getProperty:</c> evaluates properties without compiling, which
/// is why this is affordable to do twice per run.
/// </para>
/// </remarks>
public class VersionHasOneAuthorTests
{
    private const string Repo = "https://github.com/ruminabottle/dungeonmasterxiv";

    // A-7.2a. Two tags, one unchanged working tree, two versions. Against the hand-maintained
    // <Version>0.0.0.1</Version> this fails on the first assertion: both tags derived 0.0.0.1,
    // which is BUG-14 stated as something you can watch fail.
    [Fact]
    public void TwoTagsProduceTwoVersionsWithNothingHandEdited()
    {
        var first = TheBuild.VersionStampedFor("v0.1.0");
        var second = TheBuild.VersionStampedFor("v0.1.1");

        Assert.NotEqual(first, second);
        Assert.Equal("0.1.0", first);
        Assert.Equal("0.1.1", second);
    }

    // The negative control for the test above. If the csproj ignored ReleaseTag entirely and simply
    // echoed one constant, the pair above would still differ only if that constant tracked the tag
    // -- so this pins the other end: with NO tag the build must NOT invent a releasable version.
    [Fact]
    public void ABuildNobodyToldATagCarriesNoReleaseVersion()
    {
        Assert.Equal(TaggedVersion.UntaggedBuild.ToString(), Version.Parse(TheBuild.VersionStampedFor(null)).ToString());
    }

    // A-7.2a at the release tool. One built artefact cannot be published under two tags, which is
    // the same statement from the other side: a second release therefore CANNOT repeat the first's
    // version. Against the current code the second call does not throw.
    [Fact]
    public void OneBuildCannotBeReleasedUnderTwoTags()
    {
        var built = new Version(0, 1, 0, 0);

        Inputs("v0.1.0", built).Validate();

        Assert.Throws<ArgumentException>(() => Inputs("v0.1.1", built).Validate());
    }

    // A-7.2b. A tag the artefact does not agree with stops the release rather than producing a
    // manifest. All four of these exited 0 and generated a manifest when BUG-14 was filed.
    [Theory]
    [InlineData("v0.1.1")]
    [InlineData("v9.9.9")]
    [InlineData("not-a-tag-at-all")]
    [InlineData("v")]
    public void ATagTheArtefactDisagreesWithStopsTheRelease(string tag)
    {
        Assert.Throws<ArgumentException>(() => Inputs(tag, new Version(0, 1, 0, 0)).Validate());
    }

    // R-7.4a's stop clause on the case that will actually happen: somebody builds normally, then
    // runs the release tool. The refusal has to name the rebuild, not just report a mismatch.
    [Fact]
    public void AnUntaggedBuildIsRefusedAndSaysHowToFixIt()
    {
        var failure = Assert.Throws<ArgumentException>(
            () => Inputs("v0.1.0", TaggedVersion.UntaggedBuild).Validate());

        Assert.Contains("never told its tag", failure.Message, StringComparison.Ordinal);
        Assert.Contains("-p:ReleaseTag=v0.1.0", failure.Message, StringComparison.Ordinal);
    }

    // The tag and the version the build stamps are written differently -- v0.1.0 against 0.1.0.0 --
    // and comparing them unpadded would refuse every correct release.
    //
    // This used to also accept "0.1.0" and "v2.3" under the name ...HoweverTheyAreSpelled. That
    // premise was BUG-22: if several spellings of one version are all legal tags, two releases can
    // advertise one version and Dalamud never offers the second. Only the canonical spelling is a
    // tag now, and CanonicalTagTests is where that is asserted.
    [Theory]
    [InlineData("v0.1.0", 0, 1, 0, 0)]
    [InlineData("v0.0.0.1", 0, 0, 0, 1)]
    [InlineData("v1.2.3.4", 1, 2, 3, 4)]
    public void APaddedVersionStillAgreesWithTheTagThatNamesIt(
        string tag, int major, int minor, int build, int revision)
    {
        Inputs(tag, new Version(major, minor, build, revision)).Validate();
    }

    private static ReleaseInputs Inputs(string tag, Version assemblyVersion) =>
        new(tag, assemblyVersion, 13, Repo, Assets.Any());
}
