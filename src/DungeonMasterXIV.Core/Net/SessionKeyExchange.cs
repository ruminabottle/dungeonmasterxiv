using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;

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
/// derivation. Nothing here is derived from the session code — R-1.2 makes codes non-secret and
/// speakable, so a key derived from one would protect nothing.
/// </para>
/// <para>
/// <b>The EC half is BouncyCastle rather than the BCL, and that is BUG-61 (D-19).</b> On the
/// affected machines the plugin runs Windows binaries under a Wine prefix, and DMXHUM-4 measured
/// that the BCL's EC paths cannot work there <i>at all</i>: generate, import and agree all fail
/// with <c>0x80090029</c>/<c>0x80090027</c> out of the key-storage provider. The gap is not
/// confined to key STORAGE — the layer underneath cannot do EC through the provider — so no
/// arrangement of BCL calls fixes it, which is why D-11 preference (a) is eliminated by
/// measurement rather than disfavoured by taste. A managed implementation passed all six rows of
/// the same probe with no CNG anywhere.
/// </para>
/// <para>
/// <b>HKDF, the curve, and the salt are deliberately UNCHANGED and still come from the BCL.</b>
/// Each of those is on the wire, and changing one would break every client that is already
/// running. Only the EC construction moved.
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
    /// The curve both sides agree on, named once so the constructor and <see cref="CanAgreeWith(byte[])"/>
    /// cannot disagree about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NAMED domain parameters, and this is a wire decision rather than a style one.</b>
    /// <c>ECNamedDomainParameters</c> writes the curve as its OID; plain <c>ECDomainParameters</c>
    /// writes the whole curve out explicitly. Same curve, same maths, <b>different encoding</b>, and
    /// only one of them is readable by the BCL on the other side. Measured:
    /// </para>
    /// <code>
    /// ECNamedDomainParameters(OID)  ->   91-byte SPKI, .NET imports it, prefix matches .NET's own
    /// ECDomainParameters(...)       ->  335-byte SPKI, .NET refuses:
    ///                                   PlatformNotSupportedException, "Only named curves are
    ///                                   supported on this platform."
    /// </code>
    /// <para>
    /// <b>Getting this wrong presents as a wire incompatibility and is actually an export call.</b>
    /// The client generates keys nobody can read, and the search starts at the protocol, the relay
    /// and the peer — none of which is at fault.
    /// <c>TheExportedPublicKeyCarriesANamedCurveTests</c> is what stops it coming back: the Product
    /// Owner made that a release condition on the grounds that <i>a comment records this knowledge
    /// and only a test defends it</i>, since it otherwise dies at the first refactor.
    /// </para>
    /// <para>
    /// <b>The hazard that would break the validator: <see cref="CanAgreeWith(byte[])"/> is static and cannot
    /// see an instance's curve.</b> Making the curve per-instance — a constructor parameter, a
    /// negotiated curve — would leave the validator rehearsing this one while the exchange used
    /// another, and it would fail silently, admitting exactly the keys it exists to refuse. A
    /// per-instance curve requires a per-instance validator, in the same change.
    /// </para>
    /// </remarks>
    private static readonly ECNamedDomainParameters Curve = NamedCurve();

    /// <summary>
    /// Which curve implementation backs <see cref="Curve"/>, so a test can hold the fast one open.
    /// </summary>
    /// <remarks>
    /// <b>Exposed because the choice is invisible at the call site and expensive to get wrong.</b>
    /// <c>CustomNamedCurves</c> and <c>SecNamedCurves</c> return the same curve and the same
    /// encoding; only the arithmetic differs, so swapping one for the other looks like a tidy-up and
    /// changes nothing a reviewer can see. Measured on this machine, median of seven rounds:
    /// <code>
    ///                              generic FpCurve   SecP256R1Curve
    /// CanAgreeWith, valid key            995.0 us          294.4 us
    /// CanAgreeWith, wrong curve          278.4 us           58.5 us
    /// CanAgreeWith, junk bytes             2.6 us            3.0 us
    /// new SessionKeyExchange()           274.4 us           52.4 us
    /// </code>
    /// The generic implementation would have made the inbound join path <b>3.4x more expensive than
    /// the build this replaces</b> (the BCL path re-measured alongside it: 296.2 us). With the
    /// optimised one it is at parity for a valid key and several times cheaper for everything else.
    /// </remarks>
    internal static string CurveImplementation => Curve.Curve.GetType().Name;

    /// <summary>Bytes in one field element, so the agreement is fixed-width without a literal.</summary>
    /// <remarks>
    /// Derived from the curve rather than written as 32. The agreement must be the X coordinate
    /// left-padded to the field size — that padding is what makes it byte-identical to
    /// <c>DeriveRawSecretAgreement</c>, and a short big-endian integer would silently produce a
    /// different key roughly one time in 256.
    /// </remarks>
    private static readonly int FieldBytes = (Curve.Curve.FieldSize + 7) / 8;

    private readonly AsymmetricCipherKeyPair _keyPair;

    /// <summary>Creates a fresh ephemeral key pair.</summary>
    public SessionKeyExchange() => _keyPair = GenerateKeyPair();

    /// <summary>
    /// This side's public key, in SPKI form, to be placed in a join request. Safe to publish.
    /// </summary>
    public byte[] PublicKey =>
        SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(_keyPair.Public).GetDerEncoded();

    /// <summary>
    /// Whether <see cref="DeriveSharedKey"/> could actually agree with this public key (BUG-56).
    /// </summary>
    /// <param name="otherPartyPublicKey">Bytes that arrived from the wire, trusted for nothing.</param>
    /// <returns><c>true</c> only if an agreement against these bytes succeeds.</returns>
    /// <remarks>
    /// <para>
    /// <b>It answers by DOING the agreement, not by inspecting the bytes.</b> A format check is not
    /// enough and the gap is not theoretical: a well-formed SPKI blob on the WRONG CURVE — P-384,
    /// say — imports without complaint and is only refused when the agreement is attempted.
    /// Performing the operation is the only check that cannot disagree with the thing it predicts.
    /// </para>
    /// <para>
    /// <b>The guarded region covers the INPUT only, and that split is BUG-61's second half.</b>
    /// The previous version wrapped the probe-key CONSTRUCTION in the same <c>try</c> as the import,
    /// under a <c>catch</c> reading <i>"not an SPKI blob at all: junk bytes, a truncated key, an RSA
    /// key, a corrupted one"</i>. A platform failure raises the same exception type, so on an
    /// affected machine this returned <c>false</c> for <b>every key, including valid ones</b>, and
    /// logged nothing — a crash turned into a silent refusal of every peer. The crash at
    /// construction masked it, so fixing only that would have exposed it.
    /// </para>
    /// <para>
    /// So the probe key is generated <b>outside</b> the guard: a failure there is not a bad key and
    /// must propagate rather than be reported as one.
    /// <c>APlatformFailureIsNotReportedAsABadKeyTests</c> holds that open.
    /// </para>
    /// <para>
    /// <b>THIS IS STILL THE DOMINANT COST ON THE INBOUND JOIN PATH.</b> Re-measured after the move
    /// to BouncyCastle rather than carried over, because the previous figures described a different
    /// implementation. Median of seven rounds, per operation:
    /// </para>
    /// <code>
    /// CanAgreeWith, valid key      294.4 us      &lt;- this
    /// CanAgreeWith, wrong curve     58.5 us
    /// CanAgreeWith, junk bytes       3.0 us
    /// </code>
    /// <para>
    /// Roughly <b>56 valid-key requests would burn a 60fps frame</b>. That is the sentence someone
    /// will read when deciding whether this path can absorb more work, and the answer is still that
    /// it cannot absorb much — the move did not buy headroom, it held the line. See
    /// <see cref="CurveImplementation"/> for the one change that decides this.
    /// </para>
    /// <para>
    /// <b>The candidate is imported BEFORE the probe pair is created</b>, which keeps the cheapest
    /// attacker input cheap to refuse: junk bytes fail at the import and never pay for a key pair.
    /// A wrong-curve key stays more expensive, because it imports cleanly and can only be refused by
    /// attempting the agreement. That asymmetry is accepted rather than overlooked: crafting a
    /// well-formed SPKI blob on another curve is a far higher bar than sending three junk bytes.
    /// </para>
    /// <para>
    /// The agreement is discarded and zeroed. It is never key material here, only evidence that key
    /// material could be produced.
    /// </para>
    /// </remarks>
    public static bool CanAgreeWith(byte[]? otherPartyPublicKey) =>
        CanAgreeWith(otherPartyPublicKey, GenerateKeyPair);

    /// <summary>
    /// <see cref="CanAgreeWith(byte[])"/> with the probe-key generator injected, so a test can make
    /// the PLATFORM half fail and assert the failure is not reported as a bad key.
    /// </summary>
    /// <remarks>
    /// A seam rather than a mutable static: the latter would hand one test's stub to whatever thread
    /// drained next. BUG-61 is precisely the case where a platform failure was indistinguishable
    /// from bad input, so the distinction needs a test that can produce one.
    /// </remarks>
    internal static bool CanAgreeWith(
        byte[]? otherPartyPublicKey, Func<AsymmetricCipherKeyPair> generateProbeKey)
    {
        if (otherPartyPublicKey is null || otherPartyPublicKey.Length == 0)
        {
            return false;
        }

        var otherParty = TryImportPublicKey(otherPartyPublicKey);

        if (otherParty is null)
        {
            return false;
        }

        // PLATFORM WORK, deliberately outside every catch in this method. See the remarks.
        var probe = generateProbeKey();

        try
        {
            CryptographicOperations.ZeroMemory(Agree(probe.Private, otherParty));
            return true;
        }
        catch (InvalidOperationException)
        {
            // Well-formed, wrong curve. Measured: this is what P-384 and P-521 produce, as
            // "ECDH public key has wrong domain parameters".
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

        var otherParty = TryImportPublicKey(otherPartyPublicKey)
            ?? throw new CryptographicException(
                "The other party's public key is not an EC public key this session can agree with.");

        var agreement = Agree(_keyPair.Private, otherParty);
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
    /// <remarks>
    /// Nothing unmanaged is held any more — the key pair is managed objects rather than a handle on
    /// a provider — but the type stays <see cref="IDisposable"/> because callers dispose it and
    /// removing that is a separate decision.
    /// </remarks>
    public void Dispose()
    {
    }

    /// <summary>The P-256 domain parameters, written as the curve's OID rather than in full.</summary>
    private static ECNamedDomainParameters NamedCurve()
    {
        var oid = SecObjectIdentifiers.SecP256r1;
        var parameters = CustomNamedCurves.GetByOid(oid);

        return new ECNamedDomainParameters(
            oid, parameters.Curve, parameters.G, parameters.N, parameters.H, parameters.GetSeed());
    }

    /// <summary>A fresh key pair from the library's own generator.</summary>
    /// <remarks>
    /// <b>The material comes from a generator we did not write, and that is the D-11 line.</b>
    /// Importing key material from a vetted implementation is fine; choosing a private scalar
    /// ourselves and asking the platform to complete it is hand-rolled elliptic-curve cryptography,
    /// and having a library that can generate makes that shortcut easier to reach for rather than
    /// harder. There is no <c>D</c> in this file whose value we picked.
    /// </remarks>
    private static AsymmetricCipherKeyPair GenerateKeyPair()
    {
        var generator = new ECKeyPairGenerator();
        generator.Init(new ECKeyGenerationParameters(Curve, new SecureRandom()));

        return generator.GenerateKeyPair();
    }

    /// <summary>The other side's SPKI bytes as an EC public key, or <c>null</c> if they are not one.</summary>
    /// <remarks>
    /// <para>
    /// <b>The width of this catch is safe here and was not safe before.</b> Every operation inside
    /// it parses ATTACKER-SUPPLIED BYTES, so any failure means those bytes are not a public key —
    /// which is exactly what <c>null</c> says. Nothing platform-dependent happens in here, so the
    /// failure BUG-61 is about cannot be swallowed by it.
    /// </para>
    /// <para>
    /// Enumerating the types would be a check that grows a hole per unlisted exception, and the
    /// measured set is already wider than the obvious guess: three junk bytes raise
    /// <see cref="EndOfStreamException"/>, one junk byte raises <see cref="IOException"/>, and an
    /// RSA key parses successfully into a type that is not EC — which is why this tests the type
    /// instead of casting.
    /// </para>
    /// </remarks>
    private static ECPublicKeyParameters? TryImportPublicKey(byte[] otherPartyPublicKey)
    {
        try
        {
            return PublicKeyFactory.CreateKey(otherPartyPublicKey) as ECPublicKeyParameters;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The raw ECDH agreement, left-padded to the field size.</summary>
    private static byte[] Agree(ICipherParameters ourPrivateKey, ECPublicKeyParameters otherParty)
    {
        var agreement = new ECDHBasicAgreement();
        agreement.Init(ourPrivateKey);

        return BigIntegers.AsUnsignedByteArray(FieldBytes, agreement.CalculateAgreement(otherParty));
    }
}
