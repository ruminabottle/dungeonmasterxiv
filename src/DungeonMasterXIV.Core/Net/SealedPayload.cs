using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A payload that has already been encrypted. The only way to obtain one is
/// <see cref="SessionCipher.Seal"/>, so a caller cannot hand plaintext to
/// <see cref="WireEnvelope.ForSessionPayload"/> by mistake.
/// </summary>
/// <remarks>
/// This is the type-level half of A-1.5f: what leaves a member is ciphertext. The relay half —
/// that the relay holds no key — is not this chunk's to prove.
/// </remarks>
public sealed class SealedPayload
{
    internal SealedPayload(byte[] nonce, byte[] ciphertext)
    {
        Nonce = nonce;
        Ciphertext = ciphertext;
    }

    /// <summary>The per-message nonce. Never reused; see <see cref="SessionCipher.Seal"/>.</summary>
    public byte[] Nonce { get; }

    /// <summary>Ciphertext with its authentication tag appended.</summary>
    public byte[] Ciphertext { get; }

    /// <summary>
    /// Rebuilds a payload received from the wire. Separate from the internal constructor because
    /// this one is reachable by anything that parses an envelope, and the wire is untrusted —
    /// authentication happens at <see cref="SessionCipher.Open"/>, not here.
    /// </summary>
    public static SealedPayload FromWire(byte[] nonce, byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(ciphertext);
        return new SealedPayload(nonce, ciphertext);
    }
}
