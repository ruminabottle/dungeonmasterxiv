using System;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Naming for the copies kept when a campaign document could not be read.
/// </summary>
/// <remarks>
/// The convention lives here rather than in the file adapter so that the guard on
/// <see cref="IsPreservedName"/> is unit-testable. A name reaches a file delete, and the only thing
/// standing between a caller and an arbitrary path is this predicate.
/// </remarks>
public static class PreservedCampaignFile
{
    /// <summary>What every preserved file's name starts with.</summary>
    public const string Prefix = "campaigns.unreadable-";

    /// <summary>What every preserved file's name ends with.</summary>
    public const string Suffix = ".json";

    /// <summary>Builds the name to keep an unreadable document under.</summary>
    /// <param name="preservedAtUtc">When it was preserved; makes the name unique per second.</param>
    public static string NameFor(DateTimeOffset preservedAtUtc) =>
        $"{Prefix}{preservedAtUtc:yyyyMMddTHHmmssZ}{Suffix}";

    /// <summary>
    /// Whether this is a name this plugin wrote, and therefore one it may delete. Rejects anything
    /// carrying a path — a caller must not be able to reach outside the config directory by
    /// handing back a doctored name.
    /// </summary>
    /// <param name="name">A bare file name, as returned by the archive's own listing.</param>
    public static bool IsPreservedName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.Contains('/', StringComparison.Ordinal) ||
            name.Contains('\\', StringComparison.Ordinal) ||
            name.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return name.StartsWith(Prefix, StringComparison.Ordinal)
            && name.EndsWith(Suffix, StringComparison.Ordinal)
            && name.Length > Prefix.Length + Suffix.Length;
    }
}
