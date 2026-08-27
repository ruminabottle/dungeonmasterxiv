using System;

namespace DungeonMasterXIV.Release;

/// <summary>
/// The version a release tag names, e.g. <c>v0.1.0</c> to <c>0.1.0.0</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tag is the one place a human authors the advertised version (D-16, R-7.4a).</b> Everything
/// else computes from it: the build takes <c>Version</c> from <c>$(ReleaseTag)</c>, and this type is
/// what lets the release tool check that the artefact in front of it was in fact built from the tag
/// it is about to be published under.
/// </para>
/// <para>
/// <b>Why the tag and not the csproj property.</b> A-7.2a requires that a second release cut from a
/// later tag advertises a different version <i>without hand-editing anything</i>. Had the csproj
/// property stayed the author, satisfying that would mean editing the csproj — so the criterion
/// chooses the tag, and BUG-14 is what a second author cost: any tag was accepted against any build,
/// four different tags all advertised <c>0.0.0.1</c>, and Dalamud silently never offers a second
/// release that repeats the version of the first.
/// </para>
/// </remarks>
public static class TaggedVersion
{
    /// <summary>
    /// What a build reports when nobody told it a tag, i.e. no version at all.
    /// </summary>
    /// <remarks>
    /// The plugin's <c>Version</c> falls back to this whenever <c>$(ReleaseTag)</c> is not supplied,
    /// so an ordinary <c>dotnet build</c> still works and carries no release version. Any tag anyone
    /// would actually cut disagrees with it, so <see cref="ReleaseInputs.Validate"/> stops and says
    /// to rebuild with the tag. The old hand-maintained <c>0.0.0.1</c> was dangerous precisely
    /// because it read like a version somebody meant, and every build silently repeated it.
    /// </remarks>
    public static readonly Version UntaggedBuild = new(0, 0, 0, 0);

    /// <summary>
    /// The version <paramref name="tag"/> names, with unspecified components filled in as zero.
    /// </summary>
    /// <remarks>
    /// <b>Padded to four components deliberately.</b> The build turns <c>v0.1.0</c> into an assembly
    /// reporting <c>0.1.0.0</c>, while <c>Version.Parse("0.1.0")</c> leaves Revision as -1, and those
    /// two are not equal. Comparing them unpadded would refuse every correct release — an instrument
    /// that produces false failures, which is worse than one that cannot fail.
    /// </remarks>
    /// <param name="tag">The git tag, with or without its leading <c>v</c>.</param>
    /// <exception cref="ArgumentException">The tag does not name a version.</exception>
    public static Version Of(string tag)
    {
        var withoutPrefix = (tag ?? string.Empty).Trim().TrimStart('v', 'V');

        if (!Version.TryParse(withoutPrefix, out var named))
        {
            throw new ArgumentException(
                $"The release tag '{tag}' does not name a version, so nothing can be checked against " +
                "the build. The tag is the one place the advertised version is authored (R-7.4a): it " +
                "has to read like v0.1.0, because the build takes its version from it.");
        }

        return Pad(named);
    }

    /// <summary>The same version with unspecified components as zero, so two spellings compare equal.</summary>
    public static Version Pad(Version version) => new(
        version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
}
