using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// R-7.3a: the API level is copied from the built plugin manifest, never typed and never defaulted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why derivation beats confirmation here.</b> This field fails silently — a wrong level makes
/// Dalamud never offer the plugin, with nothing written anywhere on our side. Against a silent
/// failure a value nobody types is worth more than a value somebody confirms, because confirmation
/// is the step that degrades under time pressure and derivation does not.
/// </para>
/// <para>
/// <b>Two files, not one read twice.</b> The built manifest is stamped by the SDK during the build;
/// the repository manifest is produced by this tool. Comparing them tests that the tool copied
/// faithfully. Generating a manifest and reading back the same generated manifest would be one
/// source twice and could not fail — the shape closed in C16 by adding a third source.
/// </para>
/// </remarks>
public class ApiLevelIsCopiedFromTheBuildTests
{
    private static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DungeonMasterXIV.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!;
    }

    private static FileInfo BuiltManifestFile()
    {
        var bin = new DirectoryInfo(Path.Combine(RepositoryRoot().FullName, "bin"));

        var candidates = bin.Exists
            ? bin.GetFiles("DungeonMasterXIV.json", SearchOption.AllDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray()
            : Array.Empty<FileInfo>();

        Assert.True(
            candidates.Length > 0,
            "No built DungeonMasterXIV.json under bin/. R-7.3a copies the API level from the ARTEFACT, " +
            "so this fails rather than skips: build the plugin before running these tests.");

        return candidates[0];
    }

    private static PluginManifest Read(string path) =>
        JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path))!;

    // The premise, checked rather than assumed: the two manifests are DIFFERENT FILES and the build
    // is what stamps the field. If the source ever started carrying it, pointing the tool at the
    // wrong file would stop being detectable and RequireBuilt would silently pass.
    [Fact]
    public void TheSourceManifestCarriesNoApiLevelAndTheBuiltOneDoes()
    {
        var source = Read(Path.Combine(RepositoryRoot().FullName, "DungeonMasterXIV.json"));
        var built = Read(BuiltManifestFile().FullName);

        Assert.Null(source.DalamudApiLevel);
        Assert.NotNull(built.DalamudApiLevel);
        Assert.True(built.DalamudApiLevel > 0);
    }

    // R-7.3a. One side is the SDK's output, the other is this tool's. Fails if the tool hardcodes a
    // level, reads it from anywhere else, or drops it into the wrong field.
    [Fact]
    public void TheRepositoryManifestCarriesTheApiLevelTheBuildStamped()
    {
        var built = Read(BuiltManifestFile().FullName);
        var inputs = new ReleaseInputs("v0.1.0", new Version(0, 0, 0, 1), built.DalamudApiLevel!.Value, built.RepoUrl);

        using var document = JsonDocument.Parse(RepositoryManifest.Build(inputs, built));
        var entry = document.RootElement.EnumerateArray().Single();

        Assert.Equal(built.DalamudApiLevel, entry.GetProperty("TestingDalamudApiLevel").GetInt32());
        Assert.Equal(built.DalamudApiLevel, entry.GetProperty("DalamudApiLevel").GetInt32());
    }

    // Pointing at the source manifest is the mistake C16's trap describes, one step on. It must be
    // refused by name rather than producing a manifest with a field missing.
    [Fact]
    public void TheSourceManifestIsRefusedAsNotBeingABuiltOne()
    {
        var sourcePath = Path.Combine(RepositoryRoot().FullName, "DungeonMasterXIV.json");

        var failure = Assert.Throws<InvalidOperationException>(() => Read(sourcePath).RequireBuilt(sourcePath));

        // The message has to say the build did not produce what we expected, not that a number is
        // missing -- the second reads as a queue somebody clears by guessing.
        Assert.Contains("not a built plugin manifest", failure.Message, StringComparison.Ordinal);
        Assert.Contains("neither is fixed by supplying a number", failure.Message, StringComparison.Ordinal);
    }

    // The positive control for the test above. "The source manifest is refused" means nothing unless
    // a real built manifest is accepted through the same check -- otherwise RequireBuilt could be
    // refusing everything and both tests would still look right.
    [Fact]
    public void ABuiltManifestIsAcceptedByTheSameCheck()
    {
        var path = BuiltManifestFile().FullName;
        var built = Read(path);

        Assert.Same(built, built.RequireBuilt(path));
    }

    // The third source from C16, extended: the DLL and the BUILT MANIFEST are produced by different
    // build steps, so a disagreement between them means the packaging step and the compiler saw
    // different versions -- which no comparison inside either one could show.
    [Fact]
    public void TheBuiltManifestAgreesWithTheAssemblyBesideIt()
    {
        var manifestFile = BuiltManifestFile();
        var assembly = Path.Combine(manifestFile.DirectoryName!, "DungeonMasterXIV.dll");

        Assert.True(File.Exists(assembly), $"No assembly beside {manifestFile.FullName}.");

        var stamped = Read(manifestFile.FullName).AssemblyVersion;
        Assert.NotNull(stamped);
        Assert.Equal(Version.Parse(stamped!), PluginAssemblyVersion.Of(assembly));
    }
}
