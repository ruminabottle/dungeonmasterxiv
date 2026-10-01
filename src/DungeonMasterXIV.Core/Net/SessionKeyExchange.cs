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

/// <summary>An ECDH P-256 key pair that derives a 32-byte session key with another party's public key.</summary>
public sealed class SessionKeyExchange : IDisposable
{
    private static readonly byte[] DerivationInfo = "DungeonMasterXIV/session-key/v1"u8.ToArray();

    private static readonly ECNamedDomainParameters Curve = NamedCurve();

    internal static string CurveImplementation => Curve.Curve.GetType().Name;

    private static readonly int FieldBytes = (Curve.Curve.FieldSize + 7) / 8;

    private readonly AsymmetricCipherKeyPair _keyPair;

    public SessionKeyExchange() => _keyPair = GenerateKeyPair();

    public byte[] PublicKey =>
        SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(_keyPair.Public).GetDerEncoded();

    public static bool CanAgreeWith(byte[]? otherPartyPublicKey) =>
        CanAgreeWith(otherPartyPublicKey, GenerateKeyPair);

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

        var probe = generateProbeKey();

        try
        {
            CryptographicOperations.ZeroMemory(Agree(probe.Private, otherParty));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

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

    public void Dispose()
    {
    }

    private static ECNamedDomainParameters NamedCurve()
    {
        var oid = SecObjectIdentifiers.SecP256r1;
        var parameters = CustomNamedCurves.GetByOid(oid);

        return new ECNamedDomainParameters(
            oid, parameters.Curve, parameters.G, parameters.N, parameters.H, parameters.GetSeed());
    }

    private static AsymmetricCipherKeyPair GenerateKeyPair()
    {
        var generator = new ECKeyPairGenerator();
        generator.Init(new ECKeyGenerationParameters(Curve, new SecureRandom()));

        return generator.GenerateKeyPair();
    }

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

    private static byte[] Agree(ICipherParameters ourPrivateKey, ECPublicKeyParameters otherParty)
    {
        var agreement = new ECDHBasicAgreement();
        agreement.Init(ourPrivateKey);

        return BigIntegers.AsUnsignedByteArray(FieldBytes, agreement.CalculateAgreement(otherParty));
    }
}
