using System.Security.Cryptography;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The standards warn that a round-trip encrypt/decrypt in one process passes over a null cipher.
/// It would pass the round-trip test here too. It would fail every other test in this class: a null
/// cipher returns the plaintext, is deterministic, decrypts under any key, and notices no tampering.
/// </summary>
public class SessionCipherTests
{
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("initiative 17, Ysera acts first");

    private static byte[] Key() => RandomNumberGenerator.GetBytes(SessionCipher.KeySize);

    // Fails if: the cipher is a pass-through. This is the null-cipher test.
    [Fact]
    public void CiphertextIsNotThePlaintext()
    {
        var payload = SessionCipher.Seal(Key(), Plaintext);

        Assert.NotEqual(Plaintext, payload.Ciphertext);
    }

    // Fails if: the nonce is fixed or derived from the message. Encrypting the same bytes twice
    // must not be recognisable as such by a relay watching the traffic.
    [Fact]
    public void TheSamePlaintextEncryptsDifferentlyEveryTime()
    {
        var key = Key();

        var first = SessionCipher.Seal(key, Plaintext);
        var second = SessionCipher.Seal(key, Plaintext);

        Assert.NotEqual(first.Ciphertext, second.Ciphertext);
        Assert.NotEqual(first.Nonce, second.Nonce);
    }

    [Fact]
    public void TheRightKeyRecoversThePlaintext()
    {
        var key = Key();

        var recovered = SessionCipher.Open(key, SessionCipher.Seal(key, Plaintext));

        Assert.Equal(Plaintext, recovered);
    }

    // Fails if: the cipher is a pass-through, or is unauthenticated. A relay that kept a copy and
    // guessed at keys must get an exception rather than plausible-looking bytes.
    [Fact]
    public void AnotherKeyCannotOpenIt()
    {
        var payload = SessionCipher.Seal(Key(), Plaintext);

        Assert.Throws<AuthenticationTagMismatchException>(() => SessionCipher.Open(Key(), payload));
    }

    // Fails if: there is no authentication tag. This is what makes a relay unable to alter traffic
    // it forwards, rather than merely unable to read it.
    [Fact]
    public void AlteringOneByteOfCiphertextIsDetected()
    {
        var key = Key();
        var payload = SessionCipher.Seal(key, Plaintext);
        payload.Ciphertext[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => SessionCipher.Open(key, payload));
    }

    // Fails if: the nonce is not covered by the authentication.
    [Fact]
    public void AlteringOneByteOfTheNonceIsDetected()
    {
        var key = Key();
        var payload = SessionCipher.Seal(key, Plaintext);
        payload.Nonce[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => SessionCipher.Open(key, payload));
    }

    // Fails if: the tag length check is dropped and a truncated payload is treated as valid.
    [Fact]
    public void APayloadTooShortToHoldATagIsRejected()
    {
        Assert.Throws<CryptographicException>(
            () => SessionCipher.Open(Key(), SealedPayload.FromWire(new byte[SessionCipher.NonceSize], new byte[4])));
    }
}
