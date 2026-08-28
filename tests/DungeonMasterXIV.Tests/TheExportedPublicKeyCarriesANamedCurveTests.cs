using System;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The encoding half of BUG-61: our exported public key must name its curve by OID.
/// </summary>
/// <remarks>
/// <para>
/// <b>A Product Owner release condition, and it exists as a TEST rather than a comment for a stated
/// reason:</b> <i>that knowledge dies at the first refactor unless something fails when it goes. A
/// comment records it; only a test defends it.</i>
/// </para>
/// <para>
/// <b>The trap it defends against does not look like an encoding bug from where it is observed.</b>
/// <c>ECDomainParameters</c> and <c>ECNamedDomainParameters</c> describe the same curve and the same
/// arithmetic; one writes the curve out explicitly and the other writes its OID. Only the OID form
/// is readable by the BCL on the other side. Choose wrong and the client generates keys nobody can
/// read — which presents as a wire incompatibility, sending the search to the protocol, the relay
/// and the peer, none of which is at fault.
/// </para>
/// </remarks>
public class TheExportedPublicKeyCarriesANamedCurveTests
{
    // Measured under the Wine prefix and again here. Pinned because it is the value that
    // distinguishes the two encodings at a glance: the explicit form is 335 bytes.
    private const int NamedCurveSpkiLength = 91;

    [Fact]
    public void TheExportedKeyIsWhatTheOtherSideCanRead()
    {
        using var exchange = new SessionKeyExchange();

        var exported = exchange.PublicKey;

        Assert.Equal(NamedCurveSpkiLength, exported.Length);

        // The real requirement, stated as the operation rather than as a byte count: the BCL must
        // accept it. A length assertion alone would pass for 91 bytes of anything.
        using var reader = ECDiffieHellman.Create();
        var thrown = Record.Exception(() => reader.ImportSubjectPublicKeyInfo(exported, out _));

        Assert.Null(thrown);
    }

    // Stronger than "it imports": if our encoding differed from the BCL's canonical form in any way
    // it still fits, the round trip would come back different. Derived rather than pinned, so it
    // cannot be satisfied by a hand-copied byte array.
    [Fact]
    public void TheEncodingIsByteIdenticalToTheOneTheBclProduces()
    {
        using var exchange = new SessionKeyExchange();
        var exported = exchange.PublicKey;

        using var reader = ECDiffieHellman.Create();
        reader.ImportSubjectPublicKeyInfo(exported, out _);

        Assert.Equal(exported, reader.PublicKey.ExportSubjectPublicKeyInfo());
    }

    // THE CONTROL. Without it this file asserts that the thing we do works, and says nothing about
    // whether it would notice the thing we must not do. This builds the WRONG encoding on purpose
    // and shows the BCL refuses it -- so the assertions above are load-bearing rather than lucky.
    [Fact]
    public void TheExplicitCurveEncodingIsRefusedByTheOtherSide()
    {
        var oid = SecObjectIdentifiers.SecP256r1;
        var curve = SecNamedCurves.GetByOid(oid);
        var explicitDomain = new ECDomainParameters(
            curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

        var generator = new ECKeyPairGenerator();
        generator.Init(new ECKeyGenerationParameters(explicitDomain, new SecureRandom()));
        var wrongEncoding = SubjectPublicKeyInfoFactory
            .CreateSubjectPublicKeyInfo(generator.GenerateKeyPair().Public)
            .GetDerEncoded();

        // Same curve, same maths, different encoding -- and visibly a different size.
        Assert.NotEqual(NamedCurveSpkiLength, wrongEncoding.Length);

        using var reader = ECDiffieHellman.Create();

        Assert.ThrowsAny<Exception>(() => reader.ImportSubjectPublicKeyInfo(wrongEncoding, out _));
    }

    // And the curve itself is unchanged, which is a wire promise rather than an implementation
    // detail: a different curve would be a different product that cannot talk to v0.1.4.
    [Fact]
    public void TheCurveIsStillTheOneOnTheWire()
    {
        using var exchange = new SessionKeyExchange();

        using var reader = ECDiffieHellman.Create();
        reader.ImportSubjectPublicKeyInfo(exchange.PublicKey, out _);

        var parameters = reader.PublicKey.ExportParameters();

        Assert.True(parameters.Curve.IsNamed, "The curve must travel as a name, not as parameters.");
        Assert.Equal(
            ECCurve.NamedCurves.nistP256.Oid.Value,
            parameters.Curve.Oid.Value);
    }
}

/// <summary>
/// The other half of how the curve is built: which implementation does the arithmetic.
/// </summary>
/// <remarks>
/// <b>A performance decision converted into a deterministic assertion, rather than a timing test
/// that would be flaky in CI.</b> <c>CustomNamedCurves</c> and <c>SecNamedCurves</c> produce the
/// same curve, the same OID and the same SPKI bytes — every other test in this file passes either
/// way — so swapping them reads as a simplification and silently makes the inbound join path 3.4x
/// more expensive than the build being replaced. Measured: 294.4 us against 995.0 us for a valid
/// key. Nothing else here would have noticed.
/// </remarks>
public class TheCurveUsesTheOptimisedImplementationTests
{
    [Fact]
    public void TheArithmeticIsTheSpecialisedP256AndNotTheGenericFallback()
    {
        Assert.Equal("SecP256R1Curve", SessionKeyExchange.CurveImplementation);

        // Named explicitly so the failure says what went wrong rather than only what it expected:
        // FpCurve is what SecNamedCurves returns, and it is the regression this guards.
        Assert.NotEqual("FpCurve", SessionKeyExchange.CurveImplementation);
    }
}
