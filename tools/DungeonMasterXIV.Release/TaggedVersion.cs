using System;
using System.Collections.Generic;
using System.Linq;

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
    /// <param name="tag">The git tag, e.g. <c>v0.1.0</c>.</param>
    /// <exception cref="ArgumentException">
    /// The tag does not name a version, or names one it is not the canonical spelling of.
    /// </exception>
    public static Version Of(string tag)
    {
        // Read leniently, judge strictly. Stripping a capital V and surrounding whitespace here is
        // not permission to use them -- the canonical comparison below rejects both. It is so the
        // refusal can say "you meant v0.1.0" instead of "that is not a version", which is the
        // difference between ending an investigation and starting one.
        var withoutPrefix = (tag ?? string.Empty).Trim().TrimStart('v', 'V');

        if (!Version.TryParse(withoutPrefix, out var named))
        {
            throw new ArgumentException(
                $"The release tag '{tag}' does not name a version, so nothing can be checked against " +
                "the build. The tag is the one place the advertised version is authored (R-7.4a): it " +
                "has to read like v0.1.0, because the build takes its version from it.");
        }

        var padded = Pad(named);

        // BUG-25. An assembly version component caps at 65534, so a tag above that names a version
        // no artefact can ever carry -- the build refuses it and the release could never verify
        // against anything. Refused HERE as well as in the csproj because the two must agree: a tag
        // the tool accepts and the build refuses is the invariant
        // TheBuildAndTheToolReadTheTagAlikeTests exists to forbid, and making the build's refusal
        // merely prettier would have left that invariant false while a test claimed it.
        if (Components(padded).Any(component => component > AssemblyVersionComponentCap))
        {
            throw new ArgumentException(
                $"The release tag '{tag}' names version {padded}, which no assembly can carry: a " +
                $"version component cannot exceed {AssemblyVersionComponentCap}. The build refuses it " +
                "too. Pick a version whose parts are all within that range.");
        }

        var canonical = CanonicalTagFor(padded);

        // BUG-22. Tag to version is MANY-to-one: v0.1.0, v0.1.0.0, v01.2.3 and vv0.1.0 all pad to a
        // version another tag also names. Two such tags are two distinct git refs carrying two
        // distinct assets, and they advertise ONE version -- so Dalamud never offers the second,
        // which is BUG-14's consequence surviving BUG-14's fix. Requiring the canonical spelling
        // makes the aliasing unrepresentable instead of merely unlikely.
        if (!string.Equals(tag, canonical, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The release tag '{tag}' is not the canonical spelling of version {padded}; that is " +
                $"'{canonical}'. Both spellings advertise the same version, so releasing under one " +
                "after the other gives two refs, two assets and one version — and Dalamud does not " +
                "reject the second, it never offers it. Re-cut the tag as " +
                $"'{canonical}', or pick a genuinely different version.");
        }

        return padded;
    }

    /// <summary>
    /// The largest value an assembly-version component can hold. Above this the C# compiler refuses
    /// the generated <c>AssemblyVersionAttribute</c> with CS7034, so no artefact can carry it.
    /// </summary>
    private const int AssemblyVersionComponentCap = 65534;

    private static IEnumerable<int> Components(Version version) =>
        new[] { version.Major, version.Minor, version.Build, version.Revision };

    /// <summary>The one spelling of <paramref name="version"/> a release may be tagged with.</summary>
    /// <remarks>
    /// <b>Three components, or four when the fourth is not zero.</b> The build pads whatever it is
    /// given to four (<c>v0.1.0</c> is stamped <c>0.1.0.0</c>), so a version has many spellings and
    /// exactly one of them has to be legal or two tags can name it. Three is the choice because it is
    /// what every example in this repository already uses — PRD-7, <c>Program.cs</c>'s documented
    /// command, and A-7.2a's own wording all say <c>v0.1.0</c> — so the canonical form is the one
    /// people are already writing. A four-component tag stays legal when its revision is non-zero,
    /// because <c>v0.0.0.1</c> has no shorter spelling.
    /// </remarks>
    public static string CanonicalTagFor(Version version)
    {
        var padded = Pad(version);

        return padded.Revision == 0
            ? $"v{padded.Major}.{padded.Minor}.{padded.Build}"
            : $"v{padded}";
    }

    /// <summary>The same version with unspecified components as zero, so two spellings compare equal.</summary>
    public static Version Pad(Version version) => new(
        version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
}
