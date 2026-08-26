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

    private static readonly byte[] Aad =
        WireEnvelope.AssociatedDataFor(SessionCode.FromValid("BKD7RM"), WireMessageType.SessionPayload);

    private static byte[] Key() => RandomNumberGenerator.GetBytes(SessionCipher.KeySize);

    // Fails if: the cipher is a pass-through, including one careful enough to keep the lengths
    // right. The earlier version of this test asserted NotEqual(plaintext, ciphertext), which is a
    // proxy rather than the property: Ciphertext is body plus a 16-byte tag, so the two always
    // differ in length and the assertion held whatever the cipher did. The plaintext could sit in
    // the clear at offset 0 and it still passed. Assert absence, not inequality.
    [Fact]
    public void ThePlaintextDoesNotAppearInTheCiphertext()
    {
        var payload = SessionCipher.Seal(Key(), Plaintext, Aad);

        Assert.False(ByteSequence.Contains(payload.Ciphertext, Plaintext));
    }

    // Fails if: the nonce is fixed or derived from the message. Encrypting the same bytes twice
    // must not be recognisable as such by a relay watching the traffic.
    [Fact]
    public void TheSamePlaintextEncryptsDifferentlyEveryTime()
    {
        var key = Key();

        var first = SessionCipher.Seal(key, Plaintext, Aad);
        var second = SessionCipher.Seal(key, Plaintext, Aad);

        Assert.NotEqual(first.Ciphertext, second.Ciphertext);
        Assert.NotEqual(first.Nonce, second.Nonce);
    }

    [Fact]
    public void TheRightKeyRecoversThePlaintext()
    {
        var key = Key();

        var recovered = SessionCipher.Open(key, SessionCipher.Seal(key, Plaintext, Aad), Aad);

        Assert.Equal(Plaintext, recovered);
    }

    // Fails if: the cipher is a pass-through, or is unauthenticated. A relay that kept a copy and
    // guessed at keys must get an exception rather than plausible-looking bytes.
    [Fact]
    public void AnotherKeyCannotOpenIt()
    {
        var payload = SessionCipher.Seal(Key(), Plaintext, Aad);

        Assert.Throws<AuthenticationTagMismatchException>(() => SessionCipher.Open(Key(), payload, Aad));
    }

    // Fails if: there is no authentication tag. This is what makes a relay unable to alter traffic
    // it forwards, rather than merely unable to read it.
    [Fact]
    public void AlteringOneByteOfCiphertextIsDetected()
    {
        var key = Key();
        var payload = SessionCipher.Seal(key, Plaintext, Aad);
        payload.Ciphertext[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => SessionCipher.Open(key, payload, Aad));
    }

    // Fails if: the nonce is not covered by the authentication.
    [Fact]
    public void AlteringOneByteOfTheNonceIsDetected()
    {
        var key = Key();
        var payload = SessionCipher.Seal(key, Plaintext, Aad);
        payload.Nonce[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => SessionCipher.Open(key, payload, Aad));
    }

    // Fails if: the tag length check is dropped and a truncated payload is treated as valid.
    [Fact]
    public void APayloadTooShortToHoldATagIsRejected()
    {
        Assert.Throws<CryptographicException>(
            () => SessionCipher.Open(Key(), SealedPayload.FromWire(new byte[SessionCipher.NonceSize], new byte[4]), Aad));
    }
}
