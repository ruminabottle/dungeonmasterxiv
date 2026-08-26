using System;
using System.Security.Cryptography;
using System.Text;

namespace DungeonMasterXIV.Net;

/// <summary>
/// One side's ephemeral key pair for a session, and the agreement that turns the other side's
/// public key into a shared symmetric key.
/// </summary>
/// <remarks>
/// <para>
/// D-11 places this on the join flow: the joining client presents its public key with its request
/// and the DM's admission prompt shows a fingerprint of it, so the exchange adds no user-facing
/// step. This type produces the material; carrying it is <see cref="WireEnvelope"/>'s job and
/// deciding on it is the admission flow's.
/// </para>
/// <para>
/// Standard constructions only, per D-11: ECDH on NIST P-256 for agreement and HKDF-SHA256 for
/// derivation, both from the BCL. Nothing here is derived from the session code — R-1.2 makes codes
/// non-secret and speakable, so a key derived from one would protect nothing.
/// </para>
/// <para>
/// Ephemeral by construction: a new key pair per instance, never persisted. That also keeps D-8
/// intact, since a key that outlived the session would be an identifier that links a player across
/// session codes.
/// </para>
/// </remarks>
public sealed class SessionKeyExchange : IDisposable
{
    // Domain separation, so a shared secret can never be repurposed as a key for anything else.
    private static readonly byte[] DerivationInfo = "DungeonMasterXIV/session-key/v1"u8.ToArray();

    private readonly ECDiffieHellman _keyPair;

    /// <summary>Creates a fresh ephemeral key pair.</summary>
    public SessionKeyExchange() => _keyPair = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>
    /// This side's public key, in SPKI form, to be placed in a join request. Safe to publish.
    /// </summary>
    public byte[] PublicKey => _keyPair.PublicKey.ExportSubjectPublicKeyInfo();

    /// <summary>
    /// Agrees a shared key with the other side's public key, bound to one session.
    /// </summary>
    /// <param name="otherPartyPublicKey">The SPKI bytes the other side sent.</param>
    /// <param name="sessionCode">
    /// The session this key is for, used as the HKDF salt so the same pair of parties cannot derive
    /// the same key in two different sessions. Without it, whether two sessions share a key depends
    /// on how long someone happens to hold this object — a lifetime decision made elsewhere — and a
    /// past participant could read a later session's traffic.
    /// </param>
    /// <returns>A <see cref="SessionCipher.KeySize"/>-byte key. Both sides derive the same one.</returns>
    /// <remarks>
    /// The salt is not key material and does not need to be secret, which is why this is compatible
    /// with D-11: the directive forbids deriving the key <i>from</i> the code, and every bit of
    /// entropy here still comes from the ECDH agreement. Someone who knows the code and nothing else
    /// is exactly as far from the key as before. The salt binds the key to a context; it does not
    /// supply any of its strength.
    /// </remarks>
    public byte[] DeriveSharedKey(byte[] otherPartyPublicKey, SessionCode sessionCode)
    {
        ArgumentNullException.ThrowIfNull(otherPartyPublicKey);

        using var otherParty = ECDiffieHellman.Create();
        otherParty.ImportSubjectPublicKeyInfo(otherPartyPublicKey, out _);

        var agreement = _keyPair.DeriveRawSecretAgreement(otherParty.PublicKey);
        try
        {
            return HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                agreement,
                SessionCipher.KeySize,
                salt: Encoding.UTF8.GetBytes(sessionCode.Value),
                info: DerivationInfo);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(agreement);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _keyPair.Dispose();
}
