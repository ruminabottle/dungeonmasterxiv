using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The manifest's version is the version of the built assembly it links to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two sources, deliberately.</b> The manifest is generated from the built DLL, and this reads
/// the DLL again independently. It used to be three: a second test read <c>&lt;Version&gt;</c> out of
/// the csproj. That property is gone (BUG-14, D-16) — the version is now derived from the git tag,
/// so there is no declared value left to compare a build against, and the guarantee that the
/// advertised version tracks the tag moved to <see cref="VersionHasOneAuthorTests"/>.
/// </para>
/// <para>
/// <b>A-7.2 no longer names this.</b> A-7.2 asked for a match and was replaced by A-7.2a/A-7.2b,
/// because in BUG-14's reproduction the two sides matched, the criterion passed, and the release was
/// broken. What is checked here is still worth checking; it is simply not sufficient, and this file
/// no longer claims it is.
/// </para>
/// <para>
/// <b>These fail rather than skip when the plugin has not been built.</b> A skipped version check
/// reports success over an artefact that does not exist, which is the same shape as a test run that
/// executes nothing and exits zero.
/// </para>
/// </remarks>
public class ManifestMatchesTheBuiltPluginTests
{
    private static string BuiltPluginPath()
    {
        var root = TheBuild.RepositoryRoot();
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

    // One side is read out of the built assembly, the other out of the generated manifest. Fails if
    // the generator hardcodes a version, reads the project file instead of the artefact, writes it
    // into the wrong field, or renders it in a form Dalamud would not match.
    [Fact]
    public void TheManifestVersionIsTheVersionOfTheAssemblyItLinksTo()
    {
        var assemblyPath = BuiltPluginPath();
        var fromTheArtefact = PluginAssemblyVersion.Of(assemblyPath);

        var plugin = JsonSerializer.Deserialize<PluginManifest>(
            File.ReadAllText(Path.Combine(TheBuild.RepositoryRoot().FullName, "DungeonMasterXIV.json")))!;
        // The tag names whatever this tree was actually built as. A literal "v0.1.0" here would now
        // be refused against any other build -- correctly, and it would make this test about the
        // tag check rather than about the manifest. Asked for by name rather than spelt as
        // $"v{version}", because since BUG-22 only one spelling of a version is a legal tag and
        // "v0.0.0.0" is not it.
        var manifest = RepositoryManifest.Build(
            new ReleaseInputs(
                TaggedVersion.CanonicalTagFor(fromTheArtefact), fromTheArtefact, 13, plugin.RepoUrl, Assets.Any()),
            plugin);

        using var document = JsonDocument.Parse(manifest);
        var fromTheManifest = document.RootElement.EnumerateArray().Single()
            .GetProperty("TestingAssemblyVersion").GetString();

        Assert.Equal(fromTheArtefact.ToString(), fromTheManifest);

        // And the equality is not vacuous: the field carries a parseable version rather than the
        // empty string both sides would render alike. This deliberately does NOT require a release
        // version -- since BUG-14 an ordinary `dotnet build` carries TaggedVersion.UntaggedBuild,
        // and asserting otherwise would fail this suite on every tree nobody handed a tag.
        Assert.False(string.IsNullOrWhiteSpace(fromTheManifest));
        Assert.Equal(fromTheArtefact, Version.Parse(fromTheManifest!));
    }

    // The csproj must not go back to declaring a release version. That literal was BUG-14: it made
    // every build releasable under any tag, and a second release repeating it is not rejected by
    // Dalamud, merely never offered. The version it may still carry is the untagged fallback, which
    // is unreleasable by construction -- so this asserts the shape, and VersionHasOneAuthorTests
    // asserts the behaviour it produces.
    [Fact]
    public void TheProjectDeclaresNoHandAuthoredReleaseVersion()
    {
        var declared = Regex.Matches(
            File.ReadAllText(Path.Combine(TheBuild.RepositoryRoot().FullName, "DungeonMasterXIV.csproj")),
            @"<Version[^>]*>([^<]+)</Version>");

        // Without these two the loop below passes over an empty match set -- a check that cannot
        // fail, wearing the costume of one that can. The derivation must be PRESENT, not merely
        // un-contradicted.
        Assert.NotEmpty(declared);
        Assert.Contains(
            declared.Cast<Match>(),
            match => match.Groups[1].Value.Contains("$(ReleaseTag", StringComparison.Ordinal));

        foreach (Match version in declared)
        {
            var value = version.Groups[1].Value;

            Assert.True(
                value.Contains("$(ReleaseTag", StringComparison.Ordinal) ||
                Version.Parse(value) == TaggedVersion.UntaggedBuild,
                $"DungeonMasterXIV.csproj declares <Version>{value}</Version>. The advertised version " +
                "has one author and it is the git tag (D-16, R-7.4a); a literal here is a second one, " +
                "which is BUG-14.");
        }
    }

    // Reading a file that is not an assembly must fail loudly, not return a default that would make
    // the comparison above pass over nothing.
    [Fact]
    public void ReadingSomethingThatIsNotAnAssemblyFails()
    {
        var notAnAssembly = Path.Combine(TheBuild.RepositoryRoot().FullName, "DungeonMasterXIV.json");

        Assert.ThrowsAny<Exception>(() => PluginAssemblyVersion.Of(notAnAssembly));
    }

    [Fact]
    public void AMissingAssemblySaysSoRatherThanReturningAVersion()
    {
        Assert.Throws<FileNotFoundException>(() => PluginAssemblyVersion.Of("/nonexistent/DungeonMasterXIV.dll"));
    }
}
