using System;
using System.Collections.Generic;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-16: the zip carries the manifest Dalamud installs, and it must say what the repository entry
/// says.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the assembly check cannot cover this.</b> A metadata-only edit leaves the built assembly
/// byte-identical — measured independently by qa-2 and by the Deployment Manager — so a previous
/// build's zip satisfies <see cref="ReleaseAsset.MustMatchTheAssembly"/> while carrying the previous
/// build's metadata, including <c>DalamudApiLevel</c>.
/// </para>
/// <para>
/// <b>Half of this file exists to prove the guard does NOT fire.</b> Both manifests are JSON emitted
/// by different steps; key order, indentation and escaping may move without any value changing. A
/// byte comparison would pass today, because the two files happen to be byte-identical, and would
/// start failing the first time a serializer setting moved. This repository has just spent an
/// afternoon on two false-FAIL guards, and a noisy guard gets relaxed rather than fixed — so the
/// accept cases below are the load-bearing ones, not the refuse cases.
/// </para>
/// </remarks>
public class ZipManifestMatchesTheBuildTests
{
    private const string Dll = Assets.PluginAssembly;

    private static PluginManifest Built() => new()
    {
        Name = "Dungeon Master XIV",
        Author = "ruminabottle",
        Punchline = "Dice, initiative and encounter tracking for in-game tabletop sessions.",
        Description = "Tracks dice rolls, initiative, HP and session state — for tabletop RP in FFXIV.",
        RepoUrl = "https://github.com/ruminabottle/dungeonmasterxiv",
        Tags = new List<string> { "roleplay", "dice" },
        DalamudApiLevel = 15,
        AssemblyVersion = "0.1.0.0",
    };

    // The manifest the build writes, spelled the way the build spells it.
    private static string Json(PluginManifest manifest) => $$"""
        {
          "Name": "{{manifest.Name}}",
          "Author": "{{manifest.Author}}",
          "Punchline": "{{manifest.Punchline}}",
          "Description": "{{manifest.Description}}",
          "RepoUrl": "{{manifest.RepoUrl}}",
          "Tags": [{{string.Join(", ", manifest.Tags.ConvertAll(tag => $"\"{tag}\""))}}],
          "DalamudApiLevel": {{manifest.DalamudApiLevel}},
          "AssemblyVersion": "{{manifest.AssemblyVersion}}"
        }
        """;

    private static ReleaseAsset ZipCarrying(string manifestJson) =>
        ReleaseAsset.At(Assets.Zip(Assets.PackagerName, (Dll, "a build"), (Assets.PluginManifestName, manifestJson)));

    private static void Check(ReleaseAsset asset, PluginManifest built) =>
        asset.MustCarryTheSameMetadataAs(built, "bin/x64/Release/DungeonMasterXIV.json");

    // ---- the guard must NOT fire on a difference that is not a difference -------------------

    [Fact]
    public void AZipSayingTheSameThingsIsAccepted()
    {
        var built = Built();

        Check(ZipCarrying(Json(built)), built);
    }

    // THE HARDER HALF. Same values, every key in the opposite order, different indentation, and
    // trailing whitespace. A byte comparison fails this; a value comparison must not.
    [Fact]
    public void AZipWhoseManifestIsSpeltDifferentlyIsAccepted()
    {
        var built = Built();

        var reordered = $$"""
            {
                    "AssemblyVersion": "{{built.AssemblyVersion}}",
                "DalamudApiLevel": {{built.DalamudApiLevel}},
                        "Tags": [ "roleplay",   "dice" ],
              "RepoUrl": "{{built.RepoUrl}}",
                "Description": "{{built.Description}}",
                    "Punchline": "{{built.Punchline}}",
              "Author": "{{built.Author}}",
                "Name": "{{built.Name}}"
            }
            """;

        Check(ZipCarrying(reordered), built);
    }

    // Same string, different escaping: the em dash written as —. Both parse to one value, and
    // relaxed-vs-default JSON escaping is exactly the kind of thing that moves between steps.
    [Fact]
    public void AZipWhoseManifestEscapesPunctuationDifferentlyIsAccepted()
    {
        var built = Built();
        var escaped = Json(built).Replace("—", "\\u2014", StringComparison.Ordinal);

        Assert.DoesNotContain("—", escaped, StringComparison.Ordinal);

        Check(ZipCarrying(escaped), built);
    }

    // ---- the guard must fire on a difference that is one -----------------------------------

    // The headline case. A wrong API level is not rejected by Dalamud; the plugin is never offered,
    // with nothing written anywhere on our side.
    [Fact]
    public void AZipDeclaringADifferentApiLevelIsRefused()
    {
        var built = Built();
        var stale = Built();
        stale.DalamudApiLevel = 14;

        var failure = Assert.Throws<InvalidOperationException>(() => Check(ZipCarrying(Json(stale)), built));

        Assert.Contains("DalamudApiLevel", failure.Message, StringComparison.Ordinal);
        Assert.Contains("the build says '15'", failure.Message, StringComparison.Ordinal);
        Assert.Contains("the zip says '14'", failure.Message, StringComparison.Ordinal);
    }

    // Every field the repository entry republishes. A stale value in any of them makes the entry a
    // description of something other than what the user installs -- which is the whole claim.
    [Theory]
    [InlineData("Name")]
    [InlineData("Author")]
    [InlineData("Punchline")]
    [InlineData("Description")]
    [InlineData("RepoUrl")]
    [InlineData("Tags")]
    [InlineData("AssemblyVersion")]
    public void AZipStaleInAnyAdvertisedFieldIsRefused(string field)
    {
        var built = Built();
        var stale = Built();

        switch (field)
        {
            case "Name": stale.Name = "Dungeon Master XIV (old)"; break;
            case "Author": stale.Author = "someone-else"; break;
            case "Punchline": stale.Punchline = "an earlier punchline"; break;
            case "Description": stale.Description = "an earlier description"; break;
            case "RepoUrl": stale.RepoUrl = "https://github.com/ruminabottle/elsewhere"; break;
            case "Tags": stale.Tags = new List<string> { "roleplay" }; break;
            case "AssemblyVersion": stale.AssemblyVersion = "0.0.0.1"; break;
        }

        var failure = Assert.Throws<InvalidOperationException>(() => Check(ZipCarrying(Json(stale)), built));

        Assert.Contains(field, failure.Message, StringComparison.Ordinal);
    }

    // One run should end the investigation rather than starting a second one.
    [Fact]
    public void TheRefusalNamesEveryFieldThatDisagrees()
    {
        var built = Built();
        var stale = Built();
        stale.DalamudApiLevel = 14;
        stale.Punchline = "an earlier punchline";

        var failure = Assert.Throws<InvalidOperationException>(() => Check(ZipCarrying(Json(stale)), built));

        Assert.Contains("DalamudApiLevel", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Punchline", failure.Message, StringComparison.Ordinal);
    }

    // ---- the archive itself is not what we assumed ------------------------------------------

    // An explicit "Tags": null used to escape as the raw "Value cannot be null. (Parameter 'values')"
    // from string.Join, which names neither field nor file nor action. Null and an empty list mean
    // the same thing, so the zip is ACCEPTED when the build has no tags either -- and refused, by
    // name, when it has.
    [Fact]
    public void AZipSpellingNoTagsAsNullIsReadAsNoTags()
    {
        var built = Built();
        built.Tags = new List<string>();

        Check(ZipCarrying(Json(built).Replace("\"Tags\": []", "\"Tags\": null", StringComparison.Ordinal)), built);
    }

    [Fact]
    public void AZipWithNullTagsAgainstABuildThatHasThemIsRefusedByName()
    {
        var built = Built();
        var stale = Built();
        stale.Tags = new List<string>();

        var failure = Assert.Throws<InvalidOperationException>(
            () => Check(ZipCarrying(Json(stale).Replace("\"Tags\": []", "\"Tags\": null", StringComparison.Ordinal)), built));

        Assert.Contains("Tags", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Value cannot be null", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AZipWithNoManifestInItIsRefusedAsSuch()
    {
        var asset = ReleaseAsset.At(Assets.Zip(Assets.PackagerName, (Dll, "a build")));

        var failure = Assert.Throws<InvalidOperationException>(() => Check(asset, Built()));

        Assert.Contains(Assets.PluginManifestName, failure.Message, StringComparison.Ordinal);
        Assert.Contains("not a plugin release", failure.Message, StringComparison.Ordinal);
    }

    // Refusals read like refusals here too: a sentence naming the file, not a JsonException whose
    // text is about line numbers in a stream nobody can point at.
    [Fact]
    public void AZipWhoseManifestIsNotJsonIsRefusedBySentence()
    {
        var asset = ZipCarrying("this is not json");

        var failure = Assert.Throws<InvalidOperationException>(() => Check(asset, Built()));

        Assert.Contains("not readable as a plugin manifest", failure.Message, StringComparison.Ordinal);
    }
}
