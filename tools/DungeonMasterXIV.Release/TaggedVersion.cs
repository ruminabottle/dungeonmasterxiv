using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Release;

public static class TaggedVersion
{
    public static readonly Version UntaggedBuild = new(0, 0, 0, 0);

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

        var padded = Pad(named);

        if (Components(padded).Any(component => component > AssemblyVersionComponentCap))
        {
            throw new ArgumentException(
                $"The release tag '{tag}' names version {padded}, which no assembly can carry: a " +
                $"version component cannot exceed {AssemblyVersionComponentCap}. The build refuses it " +
                "too. Pick a version whose parts are all within that range.");
        }

        var canonical = CanonicalTagFor(padded);

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

    private const int AssemblyVersionComponentCap = 65534;

    private static IEnumerable<int> Components(Version version) =>
        new[] { version.Major, version.Minor, version.Build, version.Revision };

    public static string CanonicalTagFor(Version version)
    {
        var padded = Pad(version);

        return padded.Revision == 0
            ? $"v{padded.Major}.{padded.Minor}.{padded.Build}"
            : $"v{padded}";
    }

    public static Version Pad(Version version) => new(
        version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
}
