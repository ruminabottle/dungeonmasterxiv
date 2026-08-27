using System;
using System.Numerics;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The short fingerprint of a public key that D-11 requires the DM's admission prompt to show, so
/// the two humans can compare it out of band and notice if anything sat in the middle.
/// </summary>
/// <remarks>
/// <para>
/// Rendered per R-1.3a: <see cref="Characters"/> characters of
/// <see cref="SpeakableAlphabet.Characters"/>, grouped three-three-three-two. The alphabet is
/// shared with session codes rather than copied — R-1.2a's "one speakable alphabet for the whole
/// product" is the reason the fingerprint is not hex.
/// </para>
/// <para>
/// <b>This is the entire MITM defence.</b> Admission only protects a session if the DM can tell
/// the right key from a substituted one, so a forgeable fingerprint does not weaken D-11's
/// guarantee, it inverts it while the UI goes on claiming it holds.
/// </para>
/// </remarks>
public static class KeyFingerprint
{
    /// <summary>
    /// Characters shown. Eleven of a 24-symbol alphabet is ~50.4 bits (24^11).
    /// </summary>
    /// <remarks>
    /// <b>Do not change this alone.</b> R-1.3a decided eleven only because the admission prompt
    /// expires: against a bounded window, a ten-month second-preimage search is hopeless rather
    /// than merely expensive. <b>If the prompt's expiry is ever removed this must become 14</b>
    /// (~64 bits, the Code Reviewer's floor for a prompt that can sit open indefinitely). The two
    /// are a decided pair. Eleven is also a usability judgement — a DM will read aloud eight to
    /// twelve characters before people start skipping the step, and a skipped check is worse than
    /// an absent one because the UI records that it happened.
    /// </remarks>
    public const int Characters = 11;

    /// <summary>
    /// Renders a public key as a grouped, speakable fingerprint.
    /// </summary>
    /// <remarks>
    /// A SHA-256 digest, read as one unsigned big-endian integer and emitted as
    /// <see cref="Characters"/> base-24 digits.
    /// <para>
    /// <b>Why the whole digest rather than a byte per character.</b> Taking one digest byte per
    /// character and reducing it modulo 24 would bias the result: 256 is not a multiple of 24, so
    /// the first sixteen symbols would come up more often than the last eight, and eleven biased
    /// characters carry measurably less than the 50.4 bits R-1.3a's length decision assumes.
    /// <see cref="SessionCodeGenerator"/> avoids the same trap by rejection sampling, which is not
    /// available here because a fingerprint must be a deterministic function of the key. Consuming
    /// the digest as a single 256-bit value instead leaves a residual bias below 2^-205.
    /// </para>
    /// </remarks>
    /// <param name="publicKey">The SPKI bytes of the key being fingerprinted.</param>
    public static string Of(byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        var digest = SHA256.HashData(publicKey);
        var value = new BigInteger(digest, isUnsigned: true, isBigEndian: true);

        var rendered = new char[Characters];
        for (var i = Characters - 1; i >= 0; i--)
        {
            value = BigInteger.DivRem(value, SpeakableAlphabet.Length, out var symbol);
            rendered[i] = SpeakableAlphabet.Characters[(int)symbol];
        }

        return SpeakableAlphabet.Group(new string(rendered));
    }
}
