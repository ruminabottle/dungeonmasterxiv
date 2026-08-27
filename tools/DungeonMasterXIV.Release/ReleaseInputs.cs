using System;

namespace DungeonMasterXIV.Release;

/// <summary>
/// Everything the repository manifest cannot work out for itself.
/// </summary>
/// <remarks>
/// <b>Every one of these is required and none has a default.</b> A default would be a value this
/// tool invented and presented to Dalamud as fact.
/// <para>
/// <b>No value here is typed at a command line except the tag.</b> The API level and the assembly
/// version are read off the built artefact, and the asset name off the asset (R-7.3a). Paths to
/// those artefacts are typed; the values taken from them are not, which is the distinction that
/// matters — a path that is wrong fails immediately and loudly, a value that is wrong does not fail
/// at all. Against a field whose failure mode is silence — a wrong API level makes Dalamud never
/// offer the plugin, and a wrong asset name gives a tester a 404, with nothing written anywhere on
/// our side — <b>a value nobody types is worth more than a value somebody confirms.</b>
/// Confirmation is the step that degrades under time pressure; derivation does not.
/// </para>
/// </remarks>
/// <param name="Tag">The git tag of the release the download link points at, e.g. <c>v0.1.0</c>.</param>
/// <param name="AssemblyVersion">The version read out of the built plugin assembly.</param>
/// <param name="DalamudApiLevel">
/// The API level of the Dalamud this build targets, copied from the built plugin manifest. Never
/// typed and never defaulted (R-7.3a).
/// </param>
/// <param name="RepoUrl">The plugin repository, for <c>RepoUrl</c> and for building download links.</param>
/// <param name="Asset">
/// The built zip the release will carry. Supplied as a file rather than a name so that the name in
/// the download link is read off the artefact and cannot be typed (R-7.3a's rule, third outing).
/// </param>
public sealed record ReleaseInputs(
    string Tag, Version AssemblyVersion, int DalamudApiLevel, string RepoUrl, ReleaseAsset Asset)
{
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

    /// <summary>The download link for this release's asset. Always a tagged release, never a branch.</summary>
    public string DownloadLink => $"{RepoUrl.TrimEnd('/')}/releases/download/{Tag}/{Asset.Name}";
}
