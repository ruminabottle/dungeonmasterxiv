using System;

namespace DungeonMasterXIV.Release;

/// <summary>
/// Everything the repository manifest cannot work out for itself.
/// </summary>
/// <remarks>
/// <b>Every one of these is required and none has a default.</b> A default would be a value this
/// tool invented and presented to Dalamud as fact.
/// <para>
/// <b>No value here is typed at a command line except the tag, and the tag is the one place the
/// advertised version is authored (D-16, R-7.4a).</b> The API level and the assembly
/// version are read off the built artefact, and the asset name off the asset (R-7.3a). Paths to
/// those artefacts are typed; the values taken from them are not, which is the distinction that
/// matters — a path that is wrong fails immediately and loudly, a value that is wrong does not fail
/// at all. Against a field whose failure mode is silence — a wrong API level makes Dalamud never
/// offer the plugin, and a wrong asset name gives a tester a 404, with nothing written anywhere on
/// our side — <b>a value nobody types is worth more than a value somebody confirms.</b>
/// Confirmation is the step that degrades under time pressure; derivation does not.
/// </para>
/// </remarks>
/// <param name="Tag">
/// The git tag of the release, e.g. <c>v0.1.0</c>. It decides the download link's path <b>and</b> the
/// version, because the build takes <c>Version</c> from it; <see cref="Validate"/> refuses a tag the
/// artefact does not agree with rather than advertising one version and linking another.
/// </param>
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

        // BUG-14. Tag and AssemblyVersion arrive from two places and used to be compared with
        // nothing: four different tags against one unchanged build all exited 0 and all advertised
        // 0.0.0.1. Dalamud does not reject a repeated version, it simply never offers the build, so
        // the second release to a tester was silently never delivered. R-7.4a: the release stops
        // when the advertised version cannot be verified against the artefact.
        var named = TaggedVersion.Of(Tag);
        var built = TaggedVersion.Pad(AssemblyVersion);

        if (named != built)
        {
            // Naming the likely cause rather than only the two numbers. An artefact at the untagged
            // fallback is not a wrong version, it is a build nobody told the tag to -- much the
            // commonest way to arrive here, and "0.1.0 != 0.0.0.0" does not say what to do about it.
            var cause = built == TaggedVersion.UntaggedBuild
                ? $"the assembly reports {built}, which is what a build that was never told its tag carries"
                : $"the assembly was built as {built}";

            throw new ArgumentException(
                $"'{Tag}' names version {named}, but {cause}. These must agree, because Dalamud offers " +
                "an update on the version alone: a build advertising a version it was not built as is " +
                "not rejected, it is silently never offered. Rebuild with " +
                $"`dotnet build -c Release -p:ReleaseTag={Tag}`, or publish under the tag the build " +
                "already carries. Do not hand-edit the version to close the gap (D-16).");
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
