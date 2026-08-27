using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
}
