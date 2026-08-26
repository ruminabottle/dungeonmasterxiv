using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class SessionKeyExchangeTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // Fails if: the agreement is broken. Both sides deriving the same key from each other's public
    // key is the whole point; without it the DM and the joiner cannot talk.
    [Fact]
    public void BothSidesDeriveTheSameKey()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();

        Assert.Equal(dm.DeriveSharedKey(player.PublicKey, Code), player.DeriveSharedKey(dm.PublicKey, Code));
    }

    // Fails if: derivation returns a constant, ignoring the other party entirely. A shared key that
    // is the same for everyone would encrypt nothing from anyone.
    [Fact]
    public void ADifferentCounterpartyGivesADifferentKey()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();
        using var stranger = new SessionKeyExchange();

        Assert.NotEqual(dm.DeriveSharedKey(player.PublicKey, Code), dm.DeriveSharedKey(stranger.PublicKey, Code));
    }

    // Fails if: the key pair is static or persisted. D-8 requires that nothing links a player across
    // session codes, so a key that outlived one session would be exactly the identifier we refuse
    // to create.
    [Fact]
    public void EveryInstanceHasItsOwnEphemeralKeyPair()
    {
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();

        Assert.NotEqual(first.PublicKey, second.PublicKey);
    }

    // The test for finding 2. Fails if: the session code stops entering the derivation — that is,
    // if HKDF goes back to being called with no salt.
    //
    // The same two parties, the same key pair objects, two different sessions. Whether anyone holds
    // a SessionKeyExchange across a rejoin is a lifetime decision made elsewhere, so the keys must
    // differ because of what they are bound to and not because the object was short-lived. If they
    // did not, every envelope from the earlier session would decrypt in the later one and a past
    // participant could read traffic they were no longer part of.
    [Fact]
    public void TheSamePairOfPartiesDeriveDifferentKeysInDifferentSessions()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();
        var firstSession = SessionCode.FromValid("BKD7RM");
        var secondSession = SessionCode.FromValid("CFGH23");

        var inFirst = dm.DeriveSharedKey(player.PublicKey, firstSession);
        var inSecond = dm.DeriveSharedKey(player.PublicKey, secondSession);

        Assert.NotEqual(inFirst, inSecond);
    }

    // The consequence that actually matters, asserted rather than inferred: traffic sealed in one
    // session must not open in another. Fails for the same input as the test above, and states the
    // property in the terms A-1.5f is written in.
    [Fact]
    public void TrafficSealedInOneSessionDoesNotOpenInAnother()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();
        var firstSession = SessionCode.FromValid("BKD7RM");
        var secondSession = SessionCode.FromValid("CFGH23");
        var aad = WireEnvelope.AssociatedDataFor(firstSession, WireMessageType.SessionPayload);

        var sealedInFirst = SessionCipher.Seal(
            player.DeriveSharedKey(dm.PublicKey, firstSession), new byte[] { 9, 9, 9 }, aad);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => SessionCipher.Open(dm.DeriveSharedKey(player.PublicKey, secondSession), sealedInFirst, aad));
    }

    // Fails if: derivation returns something the cipher cannot use as an AES-256 key.
    [Fact]
    public void TheDerivedKeyIsTheSizeTheCipherExpects()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();

        Assert.Equal(SessionCipher.KeySize, dm.DeriveSharedKey(player.PublicKey, Code).Length);
    }

    // Fails if: the exchange and the cipher disagree about what a key is. Proves the two halves fit
    // together, which neither class can show alone.
    [Fact]
    public void AKeyFromTheExchangeActuallyDecryptsTrafficSealedWithTheOtherSidesKey()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };
        var aad = WireEnvelope.AssociatedDataFor(Code, WireMessageType.SessionPayload);

        var sealedPayload = SessionCipher.Seal(player.DeriveSharedKey(dm.PublicKey, Code), plaintext, aad);
        var recovered = SessionCipher.Open(dm.DeriveSharedKey(player.PublicKey, Code), sealedPayload, aad);

        Assert.Equal(plaintext, recovered);
    }
}
