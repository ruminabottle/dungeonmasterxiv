using System;
using System.Security.Cryptography;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-61's wire-compatibility obligation: a client built before this change and one built after it
/// must derive the SAME session key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Testable rather than argued, and that is the point.</b> ECDH is scalar multiplication of a
/// private key by a peer's public point, so the two implementations agree or they do not — there is
/// no "should be compatible" in between. A tester already running v0.1.4 must not be broken by the
/// build that fixes them.
/// </para>
/// <para>
/// <b>The shipped client is reconstructed here from the BCL, exactly as it was:</b> import the SPKI,
/// <c>DeriveRawSecretAgreement</c>, then HKDF-SHA256 with the session code as salt and the same
/// info string. If any of those four had drifted, this file would be comparing the new
/// implementation against itself and would pass while proving nothing.
/// </para>
/// <para>
/// The expected answer was measured before it was asserted: a byte-identical 32-byte secret.
/// </para>
/// </remarks>
public class TheKeyExchangeStillAgreesWithTheShippedClientTests
{
    private static readonly byte[] DerivationInfo = "DungeonMasterXIV/session-key/v1"u8.ToArray();

    [Fact]
    public void ThisBuildAndTheShippedClientDeriveTheSameKey()
    {
        var code = ACode();

        using var thisBuild = new SessionKeyExchange();
        using var shippedClient = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var fromThisBuild = thisBuild.DeriveSharedKey(
            shippedClient.PublicKey.ExportSubjectPublicKeyInfo(), code);
        var fromShippedClient = ShippedClientDerivation(shippedClient, thisBuild.PublicKey, code);

        Assert.Equal(fromThisBuild, fromShippedClient);
        Assert.Equal(SessionCipher.KeySize, fromThisBuild.Length);
    }

    // Both directions, because agreement is symmetric only if both halves encode and pad the same
    // way. A one-directional test passes on an implementation that is wrong in one direction.
    [Fact]
    public void ItAgreesInBothDirections()
    {
        var code = ACode();

        using var a = new SessionKeyExchange();
        using var b = new SessionKeyExchange();

        Assert.Equal(
            a.DeriveSharedKey(b.PublicKey, code),
            b.DeriveSharedKey(a.PublicKey, code));
    }

    // The control on the control: two DIFFERENT sessions must not agree. Without it, an
    // implementation returning a constant would satisfy every assertion above.
    [Fact]
    public void DifferentPeersDoNotAgree()
    {
        var code = ACode();

        using var a = new SessionKeyExchange();
        using var b = new SessionKeyExchange();
        using var stranger = new SessionKeyExchange();

        Assert.NotEqual(
            a.DeriveSharedKey(b.PublicKey, code),
            a.DeriveSharedKey(stranger.PublicKey, code));
    }

    // And the salt still binds the key to one session, which is the property the code supplies.
    [Fact]
    public void TheSameTwoPartiesDeriveADifferentKeyInADifferentSession()
    {
        using var a = new SessionKeyExchange();
        using var b = new SessionKeyExchange();

        Assert.NotEqual(
            a.DeriveSharedKey(b.PublicKey, ACode()),
            a.DeriveSharedKey(b.PublicKey, AnotherCode()));
    }

    /// <summary>What the previously shipped build did, written out rather than referenced.</summary>
    private static byte[] ShippedClientDerivation(
        ECDiffieHellman shippedClient, byte[] otherPartyPublicKey, SessionCode sessionCode)
    {
        using var otherParty = ECDiffieHellman.Create();
        otherParty.ImportSubjectPublicKeyInfo(otherPartyPublicKey, out _);

        var agreement = shippedClient.DeriveRawSecretAgreement(otherParty.PublicKey);
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

    // The code's alphabet is restricted, so these are two codes the parser actually accepts --
    // asserted rather than assumed, since a helper that silently produced a default would make the
    // salt constant and quietly weaken the last test in this file.
    private static SessionCode ACode() => Parse("BCDFGH");

    private static SessionCode AnotherCode() => Parse("JKMNPR");

    private static SessionCode Parse(string candidate)
    {
        Assert.True(SessionCode.TryParse(candidate, out var code), $"'{candidate}' is not a code.");
        Assert.Equal(candidate, code.Value);

        return code;
    }
}
