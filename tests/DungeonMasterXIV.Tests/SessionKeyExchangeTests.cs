using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class SessionKeyExchangeTests
{
    // Fails if: the agreement is broken. Both sides deriving the same key from each other's public
    // key is the whole point; without it the DM and the joiner cannot talk.
    [Fact]
    public void BothSidesDeriveTheSameKey()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();

        Assert.Equal(dm.DeriveSharedKey(player.PublicKey), player.DeriveSharedKey(dm.PublicKey));
    }

    // Fails if: derivation returns a constant, ignoring the other party entirely. A shared key that
    // is the same for everyone would encrypt nothing from anyone.
    [Fact]
    public void ADifferentCounterpartyGivesADifferentKey()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();
        using var stranger = new SessionKeyExchange();

        Assert.NotEqual(dm.DeriveSharedKey(player.PublicKey), dm.DeriveSharedKey(stranger.PublicKey));
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

    // Fails if: derivation returns something the cipher cannot use as an AES-256 key.
    [Fact]
    public void TheDerivedKeyIsTheSizeTheCipherExpects()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();

        Assert.Equal(SessionCipher.KeySize, dm.DeriveSharedKey(player.PublicKey).Length);
    }

    // Fails if: the exchange and the cipher disagree about what a key is. Proves the two halves fit
    // together, which neither class can show alone.
    [Fact]
    public void AKeyFromTheExchangeActuallyDecryptsTrafficSealedWithTheOtherSidesKey()
    {
        using var dm = new SessionKeyExchange();
        using var player = new SessionKeyExchange();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        var sealedPayload = SessionCipher.Seal(player.DeriveSharedKey(dm.PublicKey), plaintext);
        var recovered = SessionCipher.Open(dm.DeriveSharedKey(player.PublicKey), sealedPayload);

        Assert.Equal(plaintext, recovered);
    }
}
