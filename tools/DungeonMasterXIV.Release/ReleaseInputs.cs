using System;

namespace DungeonMasterXIV.Release;

/// <summary>
/// Everything the repository manifest cannot work out for itself.
/// </summary>
/// <remarks>
/// <b>Every one of these is required and none has a default.</b> A default here would be a value
/// invented by this tool and presented to Dalamud as fact — and the failures that produces are
/// silent: a wrong API level means Dalamud simply never offers the plugin, with nothing written
/// anywhere we would see. Refusing to generate is the only honest response to a missing input.
/// </remarks>
/// <param name="Tag">The git tag of the release the download link points at, e.g. <c>v0.1.0</c>.</param>
/// <param name="AssemblyVersion">The version read out of the built plugin assembly.</param>
/// <param name="DalamudApiLevel">
/// The API level of the Dalamud this build targets. Must be confirmed against that Dalamud release;
/// this tool will not guess it.
/// </param>
/// <param name="RepoUrl">The plugin repository, for <c>RepoUrl</c> and for building download links.</param>
public sealed record ReleaseInputs(string Tag, Version AssemblyVersion, int DalamudApiLevel, string RepoUrl)
{
    /// <summary>The asset name attached to the release, produced by DalamudPackager.</summary>
    public const string AssetName = "DungeonMasterXIV.zip";

    /// <summary>
    /// Throws unless every input is present and usable. Called before anything is generated, so a
    /// bad input fails loudly at release time instead of producing a manifest that installs nothing.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Tag))
        {
            throw new ArgumentException("A release tag is required; the download link must point at a tagged asset.");
        }

        if (DalamudApiLevel <= 0)
        {
            throw new ArgumentException(
                "A Dalamud API level is required and must be confirmed against the Dalamud release this " +
                "build targets. A wrong value makes Dalamud silently never offer the plugin.");
        }

        if (!Uri.TryCreate(RepoUrl, UriKind.Absolute, out var repo) || repo.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The repository URL must be an absolute https URL.");
        }
    }

    /// <summary>The download link for this release's asset. Always a tagged release, never a branch.</summary>
    public string DownloadLink => $"{RepoUrl.TrimEnd('/')}/releases/download/{Tag}/{AssetName}";
}
