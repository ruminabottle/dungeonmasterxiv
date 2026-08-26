using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The short fingerprint of a public key that D-11 requires the DM's admission prompt to show, so
/// the two humans can compare it out of band and notice if anything sat in the middle.
/// </summary>
public static class KeyFingerprint
{
    /// <summary>Hex characters shown. 12 characters is 48 bits of the digest.</summary>
    public const int Characters = 12;

    /// <summary>Characters per displayed group, as in <c>A1B2-C3D4-E5F6</c>.</summary>
    public const int GroupSize = 4;

    /// <summary>
    /// Renders a public key as a grouped hex fingerprint.
    /// </summary>
    /// <remarks>
    /// A truncated SHA-256 over the SPKI bytes. Truncation is what makes it short enough to read
    /// aloud, and 48 bits is the length chosen for that; it is a usability-versus-collision
    /// trade-off of the same kind R-1.2a settled for session codes, and the number is worth a
    /// product decision rather than staying an implementation detail.
    /// </remarks>
    public static string Of(byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        var digest = SHA256.HashData(publicKey);
        var hex = Convert.ToHexString(digest)[..Characters];

        var groups = new string[hex.Length / GroupSize];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = hex.Substring(i * GroupSize, GroupSize);
        }

        return string.Join('-', groups);
    }
}
