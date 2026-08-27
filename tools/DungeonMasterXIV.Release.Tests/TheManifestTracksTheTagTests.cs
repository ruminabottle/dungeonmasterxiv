using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// D-16: publishing the repository manifest is not a step a person has to remember. BUG-27: and
/// not a document a person can quietly edit.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the mechanism, not a report about one.</b> Committing <c>repo.json</c> had nothing
/// watching it, so it was skipped: every other step had a mechanism and this one had a person.
/// </para>
/// <para>
/// <b>The whole document is compared, because a list of fields is a list somebody has to keep
/// complete.</b> The first version of this check guarded four fields and
/// <c>IsTestingExclusive</c> was not one of them — so half of D-12's gate could be crossed by
/// flipping one boolean in a committed file with the suite green. Generation is deterministic, so
/// the invariant is <i>this file is what the tool produces</i>, and every field is covered by
/// consequence rather than by being remembered.
/// </para>
/// <para>
/// <b>It does not remove the hand step; it removes the silence.</b> Regenerating is still a command
/// someone runs. What changes is that forgetting it, or editing the result, turns this suite red.
/// </para>
/// </remarks>
public class TheManifestTracksTheTagTests
{
    private const string Repo = "https://github.com/ruminabottle/dungeonmasterxiv";

    // THE CHECK. Nothing here is stated: the tag comes from git, the API level off the built
    // artefact, the product copy out of the built manifest, and the expected document from the same
    // generator that wrote the committed one. Edit any field of repo.json and this goes red.
    [Fact]
    public void TheCommittedManifestIsExactlyWhatTheToolGenerates()
    {
        var tag = TheRepository.LatestReleaseTag();

        PublishedManifest.At(TheRepository.ManifestPath()).MustMatch(TheToolWouldGenerate(tag), tag);
    }

    // BUG-27's own case, as something you can watch fail rather than an argument that it would.
    // D-12's gate is a boolean in a committed file; before this it was not compared at all.
    [Fact]
    public void FlippingIsTestingExclusiveIsRefused()
    {
        var crossed = Tampered("IsTestingExclusive", false);

        var failure = Assert.Throws<InvalidOperationException>(
            () => crossed.MustMatch(AGeneratedManifest(), "v0.1.0"));

        Assert.Contains("IsTestingExclusive", failure.Message, StringComparison.Ordinal);
        Assert.Contains("D-12", failure.Message, StringComparison.Ordinal);
    }

    // The rest of the matrix the Deployment Manager reproduced. Every one of these passed before,
    // and the point of the whole-document comparison is that none of them is named anywhere.
    [Theory]
    [InlineData("InternalName", "NotTheRealPlugin")]
    [InlineData("DalamudApiLevel", 14)]
    [InlineData("TestingDalamudApiLevel", 14)]
    [InlineData("RepoUrl", "https://example.invalid/elsewhere")]
    [InlineData("TestingAssemblyVersion", "9.9.9.9")]
    [InlineData("Name", "Something Else")]
    [InlineData("ApplicableVersion", "1.2.3")]
    public void TamperingWithAnyFieldIsRefusedByName(string field, object value)
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => Tampered(field, value).MustMatch(AGeneratedManifest(), "v0.1.0"));

        Assert.Contains(field, failure.Message, StringComparison.Ordinal);
    }

    // Adding a field is a difference too. A manifest carrying something the tool never generates is
    // hand-edited by definition, and R-7.2 says that is the thing being prevented.
    [Fact]
    public void AFieldTheToolNeverGeneratesIsRefused()
    {
        var extra = Tampered("SomeFieldNobodyGenerates", "surprise");

        var failure = Assert.Throws<InvalidOperationException>(
            () => extra.MustMatch(AGeneratedManifest(), "v0.1.0"));

        Assert.Contains("SomeFieldNobodyGenerates", failure.Message, StringComparison.Ordinal);
        Assert.Contains("not generated at all", failure.Message, StringComparison.Ordinal);
    }

    // Removing one is the mirror, and it is how a link or a version goes missing entirely.
    [Fact]
    public void ADroppedFieldIsRefused()
    {
        var fields = GeneratedFields();
        fields.Remove("DownloadLinkTesting");

        var failure = Assert.Throws<InvalidOperationException>(
            () => Published(fields).MustMatch(AGeneratedManifest(), "v0.1.0"));

        Assert.Contains("DownloadLinkTesting", failure.Message, StringComparison.Ordinal);
        Assert.Contains("absent here", failure.Message, StringComparison.Ordinal);
    }

    // The positive control. Without it every test above passes for a MustMatch that throws at
    // everything, which is an instrument that cannot come out positive rather than one that works.
    [Fact]
    public void AManifestThatMatchesTheGeneratedOneIsAccepted()
    {
        Published(GeneratedFields()).MustMatch(AGeneratedManifest(), "v0.1.0");
    }

    // Key order and whitespace are not defects. A comparison that fails on them produces false
    // failures, which trains people to ignore it -- worse than one that cannot fail (BUG-16).
    [Fact]
    public void ReorderedKeysAndReformattedWhitespaceAreNotDifferences()
    {
        var reordered = new Dictionary<string, object>();
        foreach (var field in ReversedKeys())
        {
            reordered[field] = GeneratedFields()[field];
        }

        PublishedManifest.At(Assets.File("repo.json", JsonSerializer.Serialize(new[] { reordered })))
            .MustMatch(AGeneratedManifest(), "v0.1.0");
    }

    // The refusal has to carry the fix, with THIS tag substituted -- otherwise it is reconstructed
    // from memory at the one moment somebody is in a hurry, and hand-editing the file into agreement
    // is the shortcut that looks like it worked.
    [Fact]
    public void TheRefusalPrintsTheCommandsThatFixIt()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => Tampered("Name", "x").MustMatch(AGeneratedManifest(), "v0.2.0"));

        Assert.Contains("-p:ReleaseTag=v0.2.0", failure.Message, StringComparison.Ordinal);
        Assert.Contains("--tag v0.2.0 --out repo.json", failure.Message, StringComparison.Ordinal);
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

    // Unreadable must not be quieter than wrong: a manifest Dalamud cannot parse shows a tester an
    // empty repository and no error at all.
    [Theory]
    [InlineData("not json at all", "JSON")]
    [InlineData("", "JSON")]
    [InlineData("{}", "array")]
    [InlineData("[]", "array")]
    [InlineData("[{},{}]", "array")]
    [InlineData("[\"not an entry\"]", "array")]
    public void AManifestThatIsNotOneIsRefused(string content, string expected)
    {
        var failure = Assert.ThrowsAny<Exception>(
            () => PublishedManifest.At(Assets.File("repo.json", content)));

        Assert.Contains(expected, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string TheToolWouldGenerate(string tag)
    {
        var built = TheRepository.BuiltPluginManifest();

        return RepositoryManifest.Build(
            new ReleaseInputs(tag, TaggedVersion.Of(tag), built.DalamudApiLevel!.Value, built.RepoUrl, Assets.Any()),
            built);
    }

    // A generated manifest that owes nothing to the repository's own state, so the cases above are
    // about the comparison rather than about what happens to be committed today.
    private static string AGeneratedManifest() => JsonSerializer.Serialize(new[] { GeneratedFields() });

    private static Dictionary<string, object> GeneratedFields() => new(StringComparer.Ordinal)
    {
        ["Author"] = "ruminabottle",
        ["Name"] = "Dungeon Master XIV",
        ["InternalName"] = PluginManifest.InternalName,
        ["Punchline"] = "Dice and initiative.",
        ["Description"] = "A tracker.",
        ["RepoUrl"] = Repo,
        ["Tags"] = new[] { "roleplay", "tabletop" },
        ["ApplicableVersion"] = "any",
        ["IsTestingExclusive"] = true,
        ["TestingAssemblyVersion"] = "0.1.0.0",
        ["TestingDalamudApiLevel"] = 15,
        ["DownloadLinkTesting"] = $"{Repo}/releases/download/v0.1.0/latest.zip",
        ["DownloadLinkInstall"] = $"{Repo}/releases/download/v0.1.0/latest.zip",
        ["DownloadLinkUpdate"] = $"{Repo}/releases/download/v0.1.0/latest.zip",
        ["DalamudApiLevel"] = 15,
    };

    private static List<string> ReversedKeys()
    {
        var keys = new List<string>(GeneratedFields().Keys);
        keys.Reverse();
        return keys;
    }

    private static PublishedManifest Tampered(string field, object value)
    {
        var fields = GeneratedFields();
        fields[field] = value;
        return Published(fields);
    }

    private static PublishedManifest Published(Dictionary<string, object> fields) =>
        PublishedManifest.At(Assets.File("repo.json", JsonSerializer.Serialize(new[] { fields })));
}
