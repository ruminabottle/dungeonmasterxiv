using System;
using System.IO;
using System.Text.Json;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-34: the entry carries every key Dalamud declares it cannot do without.
/// </summary>
/// <remarks>
/// <para>
/// <b>This asserts against DALAMUD's requirements, not against our own output.</b> Every other check
/// on this file compares it to what the tool would generate. The tool omitted
/// <c>AssemblyVersion</c> entirely, so its output was self-consistent with the defect and every one
/// of those checks was green while a real tester was blocked. A harness that derives its
/// expectations from the product cannot see the product being wrong — third occurrence, after
/// PR #20 and BUG-16.
/// </para>
/// <para>
/// <b>Where the list comes from, and how to re-derive it.</b> Dalamud 15's
/// <c>Dalamud.Plugin.Internal.Types.PluginManifest</c> carries
/// <c>[NullableContext(2)]</c> — nullable by default — so the properties it marks
/// <c>[Nullable(1)]</c> are precisely the ones it declares must be present. Those are
/// <c>Name</c>, <c>InternalName</c>, <c>AssemblyVersion</c> and the three <c>DownloadLink</c>
/// fields. <c>TestingAssemblyVersion</c> carries no such marking and is optional — this manifest had
/// exactly those two the wrong way round. Re-derive by reflecting over
/// <c>$(DalamudLibPath)Dalamud.dll</c> with a <c>MetadataLoadContext</c>; the release tool
/// deliberately references neither Dalamud nor the plugin, which is why the result is written down
/// here rather than computed at run time.
/// </para>
/// <para>
/// <b>Present is not enough; these are typed.</b> A blank string satisfies "has the key" and still
/// gives Dalamud nothing to install, so each is required to be non-empty and
/// <c>AssemblyVersion</c> to parse as a version.
/// </para>
/// </remarks>
public class EveryKeyDalamudRequiresIsPresentTests
{
    /// <summary>The properties Dalamud 15 declares non-nullable on its own manifest type.</summary>
    public static readonly string[] Required =
    {
        "Name",
        "InternalName",
        "AssemblyVersion",
        "DownloadLinkInstall",
        "DownloadLinkUpdate",
        "DownloadLinkTesting",
    };

    public static TheoryData<string> RequiredKeys()
    {
        var keys = new TheoryData<string>();

        foreach (var key in Required)
        {
            keys.Add(key);
        }

        return keys;
    }

    // The generator. Fails on the output as it stood: AssemblyVersion was never emitted.
    [Theory]
    [MemberData(nameof(RequiredKeys))]
    public void TheGeneratedEntryCarriesEveryKeyDalamudRequires(string key)
    {
        MustCarry(Generated(), key, "the manifest the release tool generates");
    }

    // The file actually served to a tester. Separate from the generator on purpose: this is the one
    // that was wrong in production, and a check that only ever looked at freshly generated output
    // would have been green on the day the tester was blocked.
    [Theory]
    [MemberData(nameof(RequiredKeys))]
    public void TheCommittedManifestCarriesEveryKeyDalamudRequires(string key)
    {
        var path = TheRepository.ManifestPath();

        Assert.True(File.Exists(path), $"No repository manifest at '{path}'.");

        MustCarry(Entry(File.ReadAllText(path)), key, path);
    }

    // Dalamud types this as a Version, not a string, so a present-and-non-empty value that is not a
    // version leaves it exactly as unusable as an absent one.
    [Fact]
    public void TheAdvertisedVersionsParseAsVersions()
    {
        var entry = Entry(File.ReadAllText(TheRepository.ManifestPath()));

        foreach (var key in new[] { "AssemblyVersion", "TestingAssemblyVersion" })
        {
            Assert.True(
                Version.TryParse(entry.GetProperty(key).GetString(), out _),
                $"{key} is '{entry.GetProperty(key).GetString()}', which Dalamud cannot parse as a version.");
        }
    }

    // The negative control. Without it every case above passes for an assertion that cannot fail --
    // and "cannot fail" is precisely what was wrong with the checks this one is added alongside.
    [Theory]
    [MemberData(nameof(RequiredKeys))]
    public void AnEntryMissingARequiredKeyIsRefused(string key)
    {
        var withoutIt = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, JsonElement>>(
            Generated().GetRawText())!;
        withoutIt.Remove(key);

        var failure = Assert.ThrowsAny<Exception>(
            () => MustCarry(Entry(JsonSerializer.Serialize(new[] { withoutIt })), key, "a doctored entry"));

        Assert.Contains(key, failure.Message, StringComparison.Ordinal);
    }

    // And blank must be refused as firmly as absent, or the check passes on an entry Dalamud can
    // read and still cannot install from.
    [Fact]
    public void AKeyPresentButBlankIsRefused()
    {
        var blanked = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, JsonElement>>(
            Generated().GetRawText())!;
        blanked["AssemblyVersion"] = JsonSerializer.Deserialize<JsonElement>("\"   \"");

        Assert.ThrowsAny<Exception>(
            () => MustCarry(Entry(JsonSerializer.Serialize(new[] { blanked })), "AssemblyVersion", "a doctored entry"));
    }

    private static void MustCarry(JsonElement entry, string key, string describedAs)
    {
        Assert.True(
            entry.TryGetProperty(key, out var value),
            $"'{describedAs}' has no {key}. Dalamud 15 declares that property non-nullable on its " +
            "own manifest type, so an entry without it is one it cannot install from — and nothing " +
            "on our side reports that, because the tester's Dalamud is where it fails.");

        Assert.False(
            value.ValueKind == JsonValueKind.Null ||
            (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())),
            $"'{describedAs}' carries {key} but it is blank, which leaves Dalamud exactly as unable " +
            "to install as an absent one.");
    }

    private static JsonElement Entry(string manifest) =>
        JsonDocument.Parse(manifest).RootElement.EnumerateArray().GetEnumerator() is var entries && entries.MoveNext()
            ? entries.Current.Clone()
            : throw new InvalidOperationException("The manifest carries no plugin entry at all.");

    private static JsonElement Generated() => Entry(RepositoryManifest.Build(
        new ReleaseInputs("v0.1.0", new Version(0, 1, 0, 0), 15, Repo, Assets.Any()),
        new PluginManifest
        {
            Name = "Dungeon Master XIV",
            Author = "ruminabottle",
            Punchline = "Dice and initiative.",
            Description = "A tracker.",
            RepoUrl = Repo,
            DalamudApiLevel = 15,
            Tags = { "roleplay" },
        }));

    private const string Repo = "https://github.com/ruminabottle/dungeonmasterxiv";
}
