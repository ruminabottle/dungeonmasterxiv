using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// A-7.2: the manifest's version matches the version in the built assembly it links to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three sources, deliberately.</b> The manifest is generated from the built DLL; this reads the
/// DLL again independently; and a second test reads the project file. A check that consulted the
/// csproj on both sides would be comparing a source of truth with itself and could not fail, while
/// the defect it exists to catch is a manifest describing a build nobody produced.
/// </para>
/// <para>
/// <b>These fail rather than skip when the plugin has not been built.</b> A skipped version check
/// reports success over an artefact that does not exist, which is the same shape as a test run that
/// executes nothing and exits zero.
/// </para>
/// </remarks>
public class ManifestMatchesTheBuiltPluginTests
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

    private static string BuiltPluginPath()
    {
        var root = RepositoryRoot();
        var candidates = new DirectoryInfo(Path.Combine(root.FullName, "bin")).Exists
            ? new DirectoryInfo(Path.Combine(root.FullName, "bin"))
                .GetFiles("DungeonMasterXIV.dll", SearchOption.AllDirectories)
            : Array.Empty<FileInfo>();

        Assert.True(
            candidates.Length > 0,
            "No built DungeonMasterXIV.dll under bin/. A-7.2 compares the manifest against the ARTEFACT, " +
            "so this fails rather than skips. BUG-12: `dotnet test` alone never builds the plugin, because no " +
            "test project references it and that isolation is deliberate. Run `dotnet build` first, then " +
            "`dotnet test`. This tree is not broken; the command was incomplete.");

        return candidates.OrderByDescending(file => file.LastWriteTimeUtc).First().FullName;
    }

    // A-7.2. One side is read out of the built assembly, the other out of the generated manifest.
    // Fails if the generator hardcodes a version, reads the project file instead of the artefact,
    // writes it into the wrong field, or renders it in a form Dalamud would not match.
    [Fact]
    public void TheManifestVersionIsTheVersionOfTheAssemblyItLinksTo()
    {
        var assemblyPath = BuiltPluginPath();
        var fromTheArtefact = PluginAssemblyVersion.Of(assemblyPath);

        var plugin = JsonSerializer.Deserialize<PluginManifest>(
            File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "DungeonMasterXIV.json")))!;
        var manifest = RepositoryManifest.Build(
            new ReleaseInputs("v0.1.0", fromTheArtefact, 13, plugin.RepoUrl, Assets.Any()), plugin);

        using var document = JsonDocument.Parse(manifest);
        var fromTheManifest = document.RootElement.EnumerateArray().Single()
            .GetProperty("TestingAssemblyVersion").GetString();

        Assert.Equal(fromTheArtefact.ToString(), fromTheManifest);

        // And it is a real version rather than the zero both sides would show if reading the
        // assembly had quietly failed -- otherwise the equality above holds for the wrong reason.
        Assert.NotEqual(new Version(0, 0, 0, 0), fromTheArtefact);
    }

    // The third source. Catches a manifest generated from a STALE build: the DLL on disk is older
    // than the version the project now declares, so the manifest would advertise a version nobody
    // can install. Fails whenever bin/ is behind the csproj.
    [Fact]
    public void TheBuiltAssemblyIsTheVersionTheProjectDeclares()
    {
        var declared = Regex.Match(
            File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "DungeonMasterXIV.csproj")),
            @"<Version>([^<]+)</Version>");

        Assert.True(declared.Success, "DungeonMasterXIV.csproj declares no <Version>.");
        Assert.Equal(Version.Parse(declared.Groups[1].Value), PluginAssemblyVersion.Of(BuiltPluginPath()));
    }

    // Reading a file that is not an assembly must fail loudly, not return a default that would make
    // the comparison above pass over nothing.
    [Fact]
    public void ReadingSomethingThatIsNotAnAssemblyFails()
    {
        var notAnAssembly = Path.Combine(RepositoryRoot().FullName, "DungeonMasterXIV.json");

        Assert.ThrowsAny<Exception>(() => PluginAssemblyVersion.Of(notAnAssembly));
    }

    [Fact]
    public void AMissingAssemblySaysSoRatherThanReturningAVersion()
    {
        Assert.Throws<FileNotFoundException>(() => PluginAssemblyVersion.Of("/nonexistent/DungeonMasterXIV.dll"));
    }
}
