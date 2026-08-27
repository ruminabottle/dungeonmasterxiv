using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The short fingerprint of a session's key exchange — <b>one value computed from both public
/// keys</b>, which D-11 requires the admission prompt to show so the two humans can compare it out
/// of band and notice if anything sat in the middle.
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
/// <para>
/// <b>One value, not a pair, and the reason is the whole point (A-1.3f).</b> The alternative was to
/// show each side the other's fingerprint. The Product Owner rejected it because with two values to
/// check, <b>checking the first and skipping the second is the obvious shortcut — and it looks like
/// it worked.</b> A single value is symmetric, halves what has to be read aloud, and leaves no
/// second check to skip.
/// </para>
/// <para>
/// This is a <b>rendering</b> decision and deliberately not a protocol one. No wire field carries
/// it: after the exchange both sides already hold both keys, so both can compute it.
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
    /// Renders the fingerprint of a key exchange as a grouped, speakable string. Both parties get
    /// the same answer from the same two keys, whichever order they pass them in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Symmetry is the requirement, and it is achieved by canonically ordering the two keys
    /// rather than by role.</b> Ordering by role — host first, joiner second — would also produce
    /// one value, but only while every caller assigns the roles the same way; a caller that swapped
    /// them would compute a different fingerprint and the two humans would see a mismatch with
    /// nothing wrong. Ordering the blobs by their bytes makes symmetry a property of this function
    /// instead: <c>Of(a, b)</c> and <c>Of(b, a)</c> are not merely expected to agree, they take the
    /// same path. A symmetry that holds because both sides happened to call it the same way is not
    /// symmetry, it is an untested coincidence.
    /// </para>
    /// <para>
    /// <b>Both inputs are length-prefixed, and that is not decoration.</b> Concatenating two
    /// variable-length blobs is ambiguous: <c>[1,2,3]+[4,5]</c> and <c>[1,2]+[3,4,5]</c> are the
    /// same bytes, so two different exchanges could share a fingerprint. It is true that P-256 SPKI
    /// is a fixed length in practice, and a comment could rest the claim on that — but a safety
    /// argument resting on a fact no assertion holds up is exactly what this file's sibling was sent
    /// back for. The prefix makes the claim true for any key length, and a test holds it up.
    /// </para>
    /// <para>
    /// <b>Why the whole digest rather than a byte per character.</b> Taking one digest byte per
    /// character and reducing it modulo 24 would bias the result: 256 is not a multiple of 24, so
    /// the first sixteen symbols would come up more often than the last eight, and eleven biased
    /// characters carry measurably less than the 50.4 bits R-1.3a's length decision assumes.
    /// <see cref="SessionCodeGenerator"/> avoids the same trap by rejection sampling, which is not
    /// available here because a fingerprint must be a deterministic function of the keys. Consuming
    /// the digest as a single 256-bit value instead leaves a residual bias below 2^-205.
    /// </para>
    /// </remarks>
    /// <param name="oneKey">One party's SPKI public key.</param>
    /// <param name="otherKey">The other party's SPKI public key. Order does not matter.</param>
    public static string Of(byte[] oneKey, byte[] otherKey)
    {
        ArgumentNullException.ThrowIfNull(oneKey);
        ArgumentNullException.ThrowIfNull(otherKey);

        var (low, high) = oneKey.AsSpan().SequenceCompareTo(otherKey) <= 0
            ? (oneKey, otherKey)
            : (otherKey, oneKey);

        var digest = SHA256.HashData(LengthPrefixed(low, high));
        var value = new BigInteger(digest, isUnsigned: true, isBigEndian: true);

        var rendered = new char[Characters];
        for (var i = Characters - 1; i >= 0; i--)
        {
            value = BigInteger.DivRem(value, SpeakableAlphabet.Length, out var symbol);
            rendered[i] = SpeakableAlphabet.Characters[(int)symbol];
        }

        return SpeakableAlphabet.Group(new string(rendered));
    }

    // Four-byte big-endian length before each blob, so the boundary between them cannot move
    // without changing the bytes hashed.
    private static byte[] LengthPrefixed(byte[] low, byte[] high)
    {
        var buffer = new byte[sizeof(int) + low.Length + sizeof(int) + high.Length];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt32BigEndian(span, low.Length);
        low.CopyTo(span[sizeof(int)..]);

        var afterLow = sizeof(int) + low.Length;
        BinaryPrimitives.WriteInt32BigEndian(span[afterLow..], high.Length);
        high.CopyTo(span[(afterLow + sizeof(int))..]);

        return buffer;
    }
}
