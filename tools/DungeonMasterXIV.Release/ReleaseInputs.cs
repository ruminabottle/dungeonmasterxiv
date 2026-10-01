using System;

namespace DungeonMasterXIV.Release;

/// <summary>The values a repository manifest is built from, with their checks and the asset download link.</summary>
public sealed record ReleaseInputs(
    string Tag, Version AssemblyVersion, int DalamudApiLevel, string RepoUrl, ReleaseAsset Asset)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Tag))
        {
            throw new ArgumentException("A release tag is required; the download link must point at a tagged asset.");
        }

        var named = TaggedVersion.Of(Tag);
        var built = TaggedVersion.Pad(AssemblyVersion);

        if (named != built)
        {
            var cause = built == TaggedVersion.UntaggedBuild
                ? $"the assembly reports {built}, which is what a build that was never told its tag carries"
                : $"the assembly was built as {built}";

            throw new ArgumentException(
                $"'{Tag}' names version {named}, but {cause}. These must agree, because Dalamud offers " +
                "an update on the version alone: a build advertising a version it was not built as is " +
                "not rejected, it is silently never offered. Rebuild with " +
                $"`dotnet build -c Release -p:ReleaseTag={Tag}`, or publish under the tag the build " +
                "already carries. Do not hand-edit the version to close the gap.");
        }

        if (DalamudApiLevel <= 0)
        {
            throw new ArgumentException(
                "The Dalamud API level read from the built plugin manifest is not a usable value. " +
                "That means the build did not produce what we expected, which is worth investigating " +
                "— it is not something to supply by hand, because a wrong value makes Dalamud " +
                "silently never offer the plugin.");
        }

        if (!Uri.TryCreate(RepoUrl, UriKind.Absolute, out var repo) || repo.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The repository URL must be an absolute https URL.");
        }
    }

    public string DownloadLink => $"{RepoUrl.TrimEnd('/')}/releases/download/{Tag}/{Asset.Name}";
}
