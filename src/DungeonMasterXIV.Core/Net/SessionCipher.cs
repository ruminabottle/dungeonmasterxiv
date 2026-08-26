using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Encrypts and decrypts session payloads with AES-256-GCM.
/// </summary>
/// <remarks>
/// D-11 forbids a hand-rolled protocol or primitive, so every piece here is a BCL standard
/// construction: <see cref="AesGcm"/> for authenticated encryption and
/// <see cref="RandomNumberGenerator"/> for nonces. D-11 also forbids deriving a key from the
/// session code — codes are non-secret and speakable by R-1.2, so deriving from one would be
/// theatre. Keys come from <see cref="SessionKeyExchange"/> and nowhere else.
/// </remarks>
public static class SessionCipher
{
    /// <summary>Key length in bytes. AES-256.</summary>
    public const int KeySize = 32;

    /// <summary>Nonce length in bytes, as AES-GCM specifies.</summary>
    public const int NonceSize = 12;

    /// <summary>Authentication tag length in bytes.</summary>
    public const int TagSize = 16;

    /// <summary>
    /// Encrypts <paramref name="plaintext"/>, generating a fresh random nonce for every call so
    /// that encrypting the same bytes twice never produces the same ciphertext.
    /// </summary>
    public static SealedPayload Seal(byte[] key, byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var sealedBytes = new byte[ciphertext.Length + TagSize];
        ciphertext.CopyTo(sealedBytes, 0);
        tag.CopyTo(sealedBytes, ciphertext.Length);

        return new SealedPayload(nonce, sealedBytes);
    }

    /// <summary>
    /// Decrypts and authenticates a payload. Throws
    /// <see cref="AuthenticationTagMismatchException"/> if the key is wrong or the ciphertext was
    /// altered in transit — a relay that tampered would be caught here, not silently trusted.
    /// </summary>
    public static byte[] Open(byte[] key, SealedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Ciphertext.Length < TagSize)
        {
            throw new CryptographicException("Payload is shorter than its authentication tag.");
        }

        var bodyLength = payload.Ciphertext.Length - TagSize;
        var body = payload.Ciphertext.AsSpan(0, bodyLength);
        var tag = payload.Ciphertext.AsSpan(bodyLength, TagSize);
        var plaintext = new byte[bodyLength];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(payload.Nonce, body, tag, plaintext);

        return plaintext;
    }
}
