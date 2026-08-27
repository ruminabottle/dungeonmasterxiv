using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// Asks git what the repository's latest release tag is, rather than restating it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tag is the authority for the advertised version (D-16, R-7.4a), and it lives in git.</b>
/// A constant here, or a second copy in a fixture, would be a version with two authors — which is
/// BUG-14 restated, and the reason the csproj stopped owning it.
/// </para>
/// <para>
/// <b>This fails rather than skips when it cannot find a tag.</b> A shallow clone with no tags
/// fetched would otherwise make the manifest check silently vacuous, and a check that passes when
/// it cannot see its own reference point is exactly the failure the check exists to catch.
/// </para>
/// </remarks>
internal static class TheRepository
{
    /// <summary>The highest release tag in the repository, by version rather than by creation date.</summary>
    /// <remarks>
    /// <b>Highest version, not most recent ref.</b> Tags are cut in whatever order a person types
    /// them, and a hotfix tag pushed after a later release would make "most recent" name an older
    /// version. Tags this repository's own rules refuse — a non-canonical spelling, or one no
    /// assembly version can carry — are passed over rather than failing the run: refusing those is
    /// the release tool's job at the moment somebody tries to use one, not this helper's.
    /// </remarks>
    public static string LatestReleaseTag()
    {
        var (exitCode, output, errors) = Git("tag --list");

        Assert.True(exitCode == 0, $"Listing tags failed, so this check says nothing:\n{output}\n{errors}");

        var releases = Releases(output);

        Assert.True(
            releases.Count > 0,
            "No release tags in this repository, so there is nothing to check the committed " +
            "repo.json against. This FAILS rather than skips: a check that passes when it cannot " +
            "see its reference point reports success over the defect it exists to catch. If this is " +
            "a shallow clone, run `git fetch --tags`.");

        return releases[0].Tag;
    }

    private static List<(string Tag, Version Version)> Releases(string tagOutput)
    {
        var releases = new List<(string Tag, Version Version)>();

        foreach (var tag in tagOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                releases.Add((tag, TaggedVersion.Of(tag)));
            }
            catch (ArgumentException)
            {
                // Not a release tag this repository would publish under. Not this helper's to refuse.
            }
        }

        return releases.OrderByDescending(release => release.Version).ToList();
    }

    private static (int ExitCode, string Output, string Errors) Git(string arguments)
    {
        using var git = Process.Start(new ProcessStartInfo("git", $"-C \"{TheBuild.RepositoryRoot().FullName}\" {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

        var output = git.StandardOutput.ReadToEnd().Trim();
        var errors = git.StandardError.ReadToEnd().Trim();
        git.WaitForExit();

        return (git.ExitCode, output, errors);
    }

    /// <summary>The committed repository manifest, at the root where a tester's URL serves it from.</summary>
    public static string ManifestPath() => Path.Combine(TheBuild.RepositoryRoot().FullName, "repo.json");

    /// <summary>
    /// The plugin manifest as BUILT, which is the only source for the Dalamud API level.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one field the repository manifest cannot be regenerated without.</b> Thirteen of its
    /// fifteen fields come from the source manifest, the tag or the tool's own rules; the two API
    /// level fields are stamped by the SDK at build time and appear nowhere in source. An ORDINARY
    /// <c>dotnet build</c> supplies them — no Release configuration and no tag — so requiring this
    /// adds nothing beyond what BUG-12 already documents for this test project.
    /// </para>
    /// <para>
    /// <b>A second copy of this walk, deliberately.</b> <c>ApiLevelIsCopiedFromTheBuildTests</c> has
    /// the first. This file's own rule is that two copies are cheaper than a shared file and three
    /// are not, so the third one extracts them both — and a hotfix is the wrong moment to refactor a
    /// passing test.
    /// </para>
    /// </remarks>
    public static PluginManifest BuiltPluginManifest()
    {
        var bin = new DirectoryInfo(Path.Combine(TheBuild.RepositoryRoot().FullName, "bin"));

        var candidates = bin.Exists
            ? bin.GetFiles("DungeonMasterXIV.json", SearchOption.AllDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray()
            : Array.Empty<FileInfo>();

        Assert.True(
            candidates.Length > 0,
            "No built DungeonMasterXIV.json under bin/. The repository manifest is checked by " +
            "REGENERATING it, and the Dalamud API level exists only on the built artefact — so this " +
            "fails rather than skips. BUG-12: `dotnet test` alone never builds the plugin, because " +
            "no test project references it and that isolation is deliberate. Run `dotnet build` " +
            "first, then `dotnet test`. This tree is not broken; the command was incomplete.");

        var built = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(candidates[0].FullName));

        Assert.NotNull(built);
        Assert.True(
            built!.DalamudApiLevel > 0,
            $"'{candidates[0].FullName}' carries no Dalamud API level, so regenerating the " +
            "repository manifest from it would compare against a value the build did not produce.");

        return built;
    }
}
