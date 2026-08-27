using System;
using System.Linq;
using System.Text.Json;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// A-7.1 (the shape Dalamud accepts), A-7.3 (tagged asset, never a branch), and R-7.1's
/// testing-channel-only requirement.
/// </summary>
public class RepositoryManifestTests
{
    private const string Repo = "https://github.com/ruminabottle/dungeonmasterxiv";

    private static PluginManifest APlugin() => new()
    {
        Name = "Dungeon Master XIV",
        Author = "ruminabottle",
        Punchline = "Dice, initiative and encounter tracking for in-game tabletop sessions.",
        Description = "A tracker for groups running tabletop-style RP campaigns inside FFXIV.",
        RepoUrl = Repo,
        Tags = { "roleplay", "tabletop" },
    };

    private static JsonElement Entry(Version? version = null, string tag = "v0.1.0", int apiLevel = 13)
    {
        var inputs = new ReleaseInputs(tag, version ?? TaggedVersion.Of(tag), apiLevel, Repo, Assets.Any());
        var document = JsonDocument.Parse(RepositoryManifest.Build(inputs, APlugin()));

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        return document.RootElement.EnumerateArray().Single();
    }

    // A-7.1. A custom repository is a JSON ARRAY of plugin entries, not a bare object -- Dalamud
    // rejects the object form outright, and "the user saw nothing" is how that presents.
    [Fact]
    public void TheManifestIsAJsonArrayOfPluginEntries()
    {
        var manifest = RepositoryManifest.Build(
            new ReleaseInputs("v0.1.0", TaggedVersion.Of("v0.1.0"), 13, Repo, Assets.Any()), APlugin());

        using var document = JsonDocument.Parse(manifest);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Single(document.RootElement.EnumerateArray());
    }

    [Theory]
    [InlineData("Author")]
    [InlineData("Name")]
    [InlineData("InternalName")]
    [InlineData("Description")]
    [InlineData("Punchline")]
    [InlineData("RepoUrl")]
    [InlineData("ApplicableVersion")]
    [InlineData("TestingAssemblyVersion")]
    [InlineData("TestingDalamudApiLevel")]
    [InlineData("DownloadLinkTesting")]
    [InlineData("DownloadLinkInstall")]
    [InlineData("DownloadLinkUpdate")]
    [InlineData("IsTestingExclusive")]
    public void EveryFieldDalamudNeedsIsPresent(string field)
    {
        Assert.True(Entry().TryGetProperty(field, out _), $"the manifest is missing {field}");
    }

    // The permanent internal name (PRD-0 R-0.1). Fails if it is ever derived from a file name, a
    // display name or a version -- Dalamud matches an installed plugin to its manifest entry by this,
    // so a change orphans every existing install silently.
    [Fact]
    public void TheInternalNameIsThePermanentOne()
    {
        Assert.Equal("DungeonMasterXIV", Entry().GetProperty("InternalName").GetString());
    }

    // R-7.1 and D-12's second gate. Fails if the flag is dropped or written as something Dalamud
    // will not read as true.
    [Fact]
    public void TheRepositoryIsTestingExclusive()
    {
        Assert.True(Entry().GetProperty("IsTestingExclusive").GetBoolean());
    }

    // A-7.3, stated as the thing rather than a proxy for it. Fails for a branch URL, a raw link, an
    // archive tarball, or anything else that moves when someone pushes.
    [Fact]
    public void EveryDownloadLinkPointsAtATaggedReleaseAsset()
    {
        var entry = Entry(tag: "v1.2.3");

        foreach (var field in new[] { "DownloadLinkTesting", "DownloadLinkInstall", "DownloadLinkUpdate" })
        {
            var link = entry.GetProperty(field).GetString();

            Assert.NotNull(link);
            Assert.Contains("/releases/download/v1.2.3/", link!, StringComparison.Ordinal);
            Assert.DoesNotContain("/raw/", link!, StringComparison.Ordinal);
            Assert.DoesNotContain("/blob/", link!, StringComparison.Ordinal);
            Assert.DoesNotContain("/archive/", link!, StringComparison.Ordinal);
            Assert.DoesNotContain("refs/heads", link!, StringComparison.Ordinal);
        }
    }

    // The version in the manifest is whatever it was handed. Its correspondence to the BUILT
    // assembly is a separate test, because that is the half that can actually be wrong.
    [Fact]
    public void TheTestingVersionIsTheOneItWasGiven()
    {
        Assert.Equal(
            "2.3.4.5",
            Entry(version: new Version(2, 3, 4, 5), tag: "v2.3.4.5")
                .GetProperty("TestingAssemblyVersion").GetString());
    }

    // R-7.3's two PROHIBITIONS. Pinned before the required sentence existed, deliberately: someone
    // writing reassuring copy under time pressure is exactly when "verified" and "anonymous" get
    // reached for, so the guard belongs in front of that moment rather than after it.
    [Fact]
    public void TheDescriptionClaimsNeitherVerifiedRollsNorAnonymity()
    {
        var description = TheShippedDescription();

        Assert.DoesNotContain("verified", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anonym", description, StringComparison.OrdinalIgnoreCase);
    }

    // R-7.3's REQUIREMENT half, which the copy did not satisfy until the Product Owner wrote it.
    //
    // Each phrase is asserted separately so a failure names which promise was dropped rather than
    // reporting that "the description changed". They are not decorative synonyms:
    //   "nothing on its own"      -- the surprise a user would otherwise get after installing
    //   "plugin installed"        -- a REQUIREMENT of the other players, not a description of them
    //   "relay"                   -- the infrastructure dependency, R-7.3 names it explicitly
    //   "cannot take part"        -- the CONSEQUENCE. Under the original chat-derived design a
    //                                player without the plugin could still have rolled /random and
    //                                been captured; the transport reversal ended that and nobody
    //                                had written down that it had ended. This sentence closes that
    //                                stale premise, so it is the one least safe to drop.
    [Theory]
    [InlineData("nothing on its own")]
    [InlineData("plugin installed")]
    [InlineData("relay")]
    [InlineData("cannot take part")]
    public void TheDescriptionStatesWhatThePluginNeedsToBeUsableAtAll(string promise)
    {
        Assert.Contains(promise, TheShippedDescription(), StringComparison.OrdinalIgnoreCase);
    }

    // Read from the SHIPPED plugin manifest, not from the fixture above: these two tests are about
    // the copy that actually reaches a user, and a fixture would only assert that this test file
    // agrees with itself.
    private static string TheShippedDescription()
    {
        var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "DungeonMasterXIV.sln")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var plugin = JsonSerializer.Deserialize<PluginManifest>(
            System.IO.File.ReadAllText(System.IO.Path.Combine(root!.FullName, "DungeonMasterXIV.json")));

        Assert.NotNull(plugin);
        return plugin!.Description;
    }

    // Generating is refused outright when an input is missing, rather than producing a manifest with
    // a plausible default in it.
    [Fact]
    public void AManifestIsNotProducedFromIncompleteInputs()
    {
        var inputs = new ReleaseInputs(string.Empty, new Version(1, 0), 13, Repo, Assets.Any());

        Assert.Throws<ArgumentException>(() => RepositoryManifest.Build(inputs, APlugin()));
    }
}
