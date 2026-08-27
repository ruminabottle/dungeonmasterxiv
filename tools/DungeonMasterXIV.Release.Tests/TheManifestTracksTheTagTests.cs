using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// D-16: publishing the repository manifest is not a step a person has to remember.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the mechanism, not a report about one.</b> Committing <c>repo.json</c> had nothing
/// watching it, so it was skipped: the release was cut, the download link verified, and the manifest
/// never committed at all. Every other check was green, because every other step had a mechanism and
/// this one had a person.
/// </para>
/// <para>
/// <b>It does not remove the hand step; it removes the silence.</b> Regenerating is still a command
/// someone runs. What changes is that forgetting it now turns this suite red instead of shipping a
/// manifest that disagrees with the release — and a stale manifest is worse than the absent one this
/// replaces, because an absent manifest 404s loudly while a stale one resolves, offers a version the
/// release does not carry, and looks like it worked.
/// </para>
/// </remarks>
public class TheManifestTracksTheTagTests
{
    private const string Repo = "https://github.com/ruminabottle/dungeonmasterxiv";

    // THE CHECK. Both sides are read rather than stated: the version out of the committed file, the
    // tag out of git. Cut a tag without regenerating repo.json and this goes red.
    [Fact]
    public void TheCommittedManifestAdvertisesTheVersionOfTheLatestTag()
    {
        PublishedManifest.At(TheRepository.ManifestPath()).MustDescribeTheReleaseTagged(TheRepository.LatestReleaseTag());
    }

    // The failing input, stated as something you can watch fail rather than argued for. A manifest
    // left at the previous release while the tag moved on is the whole defect.
    [Fact]
    public void AManifestLeftBehindByANewTagIsRefused()
    {
        var stale = Published("0.1.0.0");

        var failure = Assert.Throws<InvalidOperationException>(() => stale.MustDescribeTheReleaseTagged("v0.2.0"));

        Assert.Contains("0.1.0.0", failure.Message, StringComparison.Ordinal);
        Assert.Contains("v0.2.0", failure.Message, StringComparison.Ordinal);

        // The refusal has to carry the fix, not just the diagnosis, and with THIS tag substituted --
        // otherwise it is reconstructed from memory at the one moment somebody is in a hurry, and
        // hand-editing the version into agreement is the shortcut that looks like it worked.
        Assert.Contains("-p:ReleaseTag=v0.2.0", failure.Message, StringComparison.Ordinal);
        Assert.Contains("--tag v0.2.0 --out repo.json", failure.Message, StringComparison.Ordinal);
    }

    // The hole the version check alone leaves open, and the reason the links are checked too. Bump
    // TestingAssemblyVersion by hand and this manifest agrees with the tag on version while still
    // fetching the PREVIOUS release's asset -- so a tester installs the old build under the new
    // number, and Dalamud, believing them current, never offers the real one.
    [Fact]
    public void AManifestWhoseVersionWasEditedButLinksWereNotIsRefused()
    {
        var handEdited = Published("0.2.0.0", linkTag: "v0.1.0");

        var failure = Assert.Throws<InvalidOperationException>(
            () => handEdited.MustDescribeTheReleaseTagged("v0.2.0"));

        Assert.Contains("v0.1.0", failure.Message, StringComparison.Ordinal);
        Assert.Contains("older", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    // A manifest missing a link field advertises a version Dalamud cannot fetch, which it reports
    // by offering nothing.
    [Theory]
    [InlineData("DownloadLinkTesting")]
    [InlineData("DownloadLinkInstall")]
    [InlineData("DownloadLinkUpdate")]
    public void AManifestMissingADownloadLinkIsRefusedByField(string field)
    {
        var fields = new Dictionary<string, string>
        {
            ["TestingAssemblyVersion"] = "0.2.0.0",
            ["DownloadLinkTesting"] = Link("v0.2.0"),
            ["DownloadLinkInstall"] = Link("v0.2.0"),
            ["DownloadLinkUpdate"] = Link("v0.2.0"),
        };
        fields.Remove(field);

        var failure = Assert.Throws<InvalidOperationException>(
            () => PublishedManifest.At(Assets.File("repo.json", AnEntry(fields))));

        Assert.Contains(field, failure.Message, StringComparison.Ordinal);
    }

    // The positive control. Without it the tests above pass for a check that throws at everything,
    // which would be an instrument that cannot come out positive rather than one that works.
    [Fact]
    public void AManifestThatMatchesTheTagIsAccepted()
    {
        Published("0.2.0.0").MustDescribeTheReleaseTagged("v0.2.0");
    }

    // v0.2.0 stamps 0.2.0.0, and Version.Parse("0.2.0") leaves Revision at -1. Comparing those
    // unpadded refuses every correct release, which is an instrument that produces false failures --
    // worse than one that cannot fail, because it trains people to ignore it.
    [Fact]
    public void AThreeComponentTagMatchesTheFourComponentVersionItStamps()
    {
        Published("0.2.0.0").MustDescribeTheReleaseTagged("v0.2.0");
        Assert.Equal(new Version(0, 2, 0, 0), Published("0.2.0").AdvertisedVersion);
    }

    // The absence this whole chunk exists because of. A check that shrugs at a missing manifest
    // passes on precisely the failure it was written for.
    [Fact]
    public void AManifestThatIsNotThereFailsRatherThanPassing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-directory-here", "repo.json");

        var failure = Assert.Throws<FileNotFoundException>(() => PublishedManifest.At(missing));

        Assert.Contains(missing, failure.Message, StringComparison.Ordinal);
    }

    // Every remaining way of not having a usable version. Each must stop rather than resolve to
    // something -- a manifest Dalamud cannot read shows a tester an empty repository and no error,
    // so "unreadable" must not be quieter than "wrong".
    [Theory]
    [InlineData("not json at all", "JSON")]
    [InlineData("{}", "array")]
    [InlineData("[]", "array")]
    [InlineData("[{},{}]", "array")]
    [InlineData("[{\"Name\":\"x\"}]", "TestingAssemblyVersion")]
    [InlineData("[{\"TestingAssemblyVersion\":\"not-a-version\"}]", "not a version")]
    [InlineData("[{\"TestingAssemblyVersion\":\"0.2.0.0\"}]", "DownloadLinkTesting")]
    public void AManifestWithNoUsableVersionIsRefusedBySymptom(string content, string expected)
    {
        var path = Assets.File("repo.json", content);

        var failure = Assert.ThrowsAny<Exception>(() => PublishedManifest.At(path));

        Assert.Contains(expected, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(path, failure.Message, StringComparison.Ordinal);
    }

    // Both halves are settable so a test can put them OUT of step, which is the only way to write a
    // case for a manifest whose version and links disagree.
    private static PublishedManifest Published(string advertisedVersion, string linkTag = "v0.2.0") =>
        PublishedManifest.At(Assets.File("repo.json", AnEntry(new Dictionary<string, string>
        {
            ["Name"] = "Dungeon Master XIV",
            ["RepoUrl"] = Repo,
            ["TestingAssemblyVersion"] = advertisedVersion,
            ["DownloadLinkTesting"] = Link(linkTag),
            ["DownloadLinkInstall"] = Link(linkTag),
            ["DownloadLinkUpdate"] = Link(linkTag),
        })));

    private static string Link(string tag) => $"{Repo}/releases/download/{tag}/latest.zip";

    // Serialized rather than concatenated. The first version of this built the JSON by adding string
    // literals and then calling Replace on the result -- which binds to the LAST literal only, so a
    // field in the first one was never removed and the case passed by never testing anything.
    private static string AnEntry(Dictionary<string, string> fields) =>
        JsonSerializer.Serialize(new[] { fields });
}
