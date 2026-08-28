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

    /// <summary>
    /// The curve both sides agree on, named once so the constructor and <see cref="CanAgreeWith"/>
    /// cannot disagree about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shared constant is the weaker half of the guarantee.</b> The stronger half is that
    /// <see cref="CanAgreeWith"/> makes the <i>identical two calls</i>
    /// <see cref="DeriveSharedKey"/> makes — <c>ImportSubjectPublicKeyInfo</c> then
    /// <c>DeriveRawSecretAgreement</c>, on this curve. It is a faithful rehearsal of the operation
    /// rather than an approximation of it, so there is no predicate that could be right about the
    /// format and wrong about the outcome.
    /// </para>
    /// <para>
    /// <b>The hazard that would break it: <see cref="CanAgreeWith"/> is static and cannot see an
    /// instance's curve.</b> Making the curve per-instance — a constructor parameter, a negotiated
    /// curve — would leave the validator rehearsing this one while the exchange used another, and it
    /// would fail silently, admitting exactly the keys it exists to refuse. A per-instance curve
    /// requires a per-instance validator, in the same change.
    /// </para>
    /// </remarks>
    private static readonly ECCurve Curve = ECCurve.NamedCurves.nistP256;

    private readonly ECDiffieHellman _keyPair;

    /// <summary>Creates a fresh ephemeral key pair.</summary>
    public SessionKeyExchange() => _keyPair = ECDiffieHellman.Create(Curve);

    /// <summary>
    /// This side's public key, in SPKI form, to be placed in a join request. Safe to publish.
    /// </summary>
    public byte[] PublicKey => _keyPair.PublicKey.ExportSubjectPublicKeyInfo();

    /// <summary>
    /// Whether <see cref="DeriveSharedKey"/> could actually agree with this public key (BUG-56).
    /// </summary>
    /// <param name="otherPartyPublicKey">Bytes that arrived from the wire, trusted for nothing.</param>
    /// <returns><c>true</c> only if an agreement against these bytes succeeds.</returns>
    /// <remarks>
    /// <para>
    /// <b>It answers by DOING the agreement, not by inspecting the bytes.</b> A format check is not
    /// enough and the gap is not theoretical: a well-formed SPKI blob on the WRONG CURVE — P-384, say
    /// — imports without complaint and fails afterwards inside
    /// <see cref="ECDiffieHellman.DeriveRawSecretAgreement"/> with an
    /// <see cref="ArgumentException"/>, which is not the <see cref="CryptographicException"/> a
    /// caller guarding its own derivation would think to catch. Performing the operation is the only
    /// check that cannot disagree with the thing it predicts.
    /// </para>
    /// <para>
    /// <b>THIS IS NOW THE DOMINANT COST ON THE INBOUND JOIN PATH.</b> Measured on the shipping
    /// build, minimum of seven interleaved rounds, per operation:
    /// </para>
    /// <code>
    /// CanAgreeWith, valid key      306.4 us      &lt;- this
    /// CanAgreeWith, wrong curve    236.3 us
    /// CanAgreeWith, junk bytes       3.8 us
    /// KeyFingerprint.Of             15.2 us
    /// EnvelopeCodec.Encode           0.1 us
    /// </code>
    /// <para>
    /// A valid key costs <b>~20x the fingerprint and ~3600x the frame encode</b> that the same path
    /// already performs. It is not a rounding error beside them; it is one to three orders of
    /// magnitude above them, and roughly <b>54 valid-key requests would burn a 60fps frame</b>.
    /// Stated in these terms deliberately: this is the sentence someone will read when deciding
    /// whether this path can absorb more work, and the answer is that it cannot absorb much.
    /// </para>
    /// <para>
    /// <b>The candidate is imported BEFORE the probe pair is created</b>, which is what keeps the
    /// cheapest attacker input cheap to refuse: junk bytes fail at the import and never pay for a
    /// key pair. Creating the pair first cost <b>215.8 us</b> for three junk bytes against
    /// <b>3.8 us</b> now — a 57x saving on the input that costs an attacker nothing to send.
    /// <b>A wrong-curve key stays expensive</b>, because it imports cleanly and can only be refused
    /// by attempting the agreement. That asymmetry is accepted rather than overlooked: crafting a
    /// well-formed SPKI blob on another curve is a far higher bar than sending three junk bytes.
    /// </para>
    /// <para>
    /// The alternative, caching a probe key in a static field, would hand a type with no
    /// thread-safety guarantee to whatever thread drains next.
    /// </para>
    /// <para>
    /// The agreement is discarded and zeroed. It is never key material here, only evidence that key
    /// material could be produced.
    /// </para>
    /// </remarks>
    public static bool CanAgreeWith(byte[]? otherPartyPublicKey)
    {
        if (otherPartyPublicKey is null || otherPartyPublicKey.Length == 0)
        {
            return false;
        }

        try
        {
            // Import first, probe second. Generating the probe pair up front made three junk bytes
            // cost almost as much as a real key; failing at the import costs 3.8 us instead of 215.8.
            using var otherParty = ECDiffieHellman.Create();
            otherParty.ImportSubjectPublicKeyInfo(otherPartyPublicKey, out _);
            using var probe = ECDiffieHellman.Create(Curve);
            CryptographicOperations.ZeroMemory(probe.DeriveRawSecretAgreement(otherParty.PublicKey));
            return true;
        }
        catch (CryptographicException)
        {
            // Not an SPKI blob at all: junk bytes, a truncated key, an RSA key, a corrupted one.
            return false;
        }
        catch (ArgumentException)
        {
            // Well-formed, wrong curve. Measured: this is what P-384 and P-521 produce.
            return false;
        }
    }

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
