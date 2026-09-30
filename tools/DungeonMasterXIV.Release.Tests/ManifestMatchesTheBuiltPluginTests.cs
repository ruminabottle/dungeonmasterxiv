using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>The release manifest's version is the version of the built plugin assembly it links to.</summary>
public class ManifestMatchesTheBuiltPluginTests
{
    [Fact]
    public void TheManifestVersionIsTheVersionOfTheAssemblyItLinksTo()
    {
        var root = TheBuild.RepositoryRoot().FullName;
        var binDirectory = new DirectoryInfo(Path.Combine(root, "bin"));
        var built = binDirectory.Exists
            ? binDirectory.GetFiles("DungeonMasterXIV.dll", SearchOption.AllDirectories)
            : Array.Empty<FileInfo>();
        Assert.True(built.Length > 0, "No built DungeonMasterXIV.dll under bin/. Run `dotnet build` before `dotnet test`.");

        var fromTheArtefact = PluginAssemblyVersion.Of(built.OrderByDescending(file => file.LastWriteTimeUtc).First().FullName);
        var plugin = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(Path.Combine(root, "DungeonMasterXIV.json")))!;
        var manifest = RepositoryManifest.Build(
            new ReleaseInputs(
                TaggedVersion.CanonicalTagFor(fromTheArtefact), fromTheArtefact, 13, plugin.RepoUrl, Assets.Any()),
            plugin);

        using var document = JsonDocument.Parse(manifest);
        var fromTheManifest = document.RootElement.EnumerateArray().Single()
            .GetProperty("TestingAssemblyVersion").GetString();

        Assert.False(string.IsNullOrWhiteSpace(fromTheManifest));
        Assert.Equal(fromTheArtefact, Version.Parse(fromTheManifest!));
    }
}
