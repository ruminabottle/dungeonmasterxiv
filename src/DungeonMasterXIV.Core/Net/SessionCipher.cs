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
    /// <param name="key">The shared key from <see cref="SessionKeyExchange.DeriveSharedKey"/>.</param>
    /// <param name="plaintext">The bytes to encrypt.</param>
    /// <param name="associatedData">
    /// Envelope metadata to authenticate but not encrypt — see
    /// <see cref="WireEnvelope.AssociatedDataFor"/>. It is covered by the tag and never transmitted;
    /// the receiver recomputes it from the fields it already has. Without it the tag covers only the
    /// payload, and a relay could re-stamp a valid payload with a different message type or session
    /// code and have it still verify.
    /// </param>
    public static SealedPayload Seal(byte[] key, byte[] plaintext, byte[] associatedData)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(associatedData);
        RequireDocumentedKeySize(key);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        var sealedBytes = new byte[ciphertext.Length + TagSize];
        ciphertext.CopyTo(sealedBytes, 0);
        tag.CopyTo(sealedBytes, ciphertext.Length);

        return new SealedPayload(nonce, sealedBytes);
    }

    /// <summary>
    /// Decrypts and authenticates a payload. Throws
    /// <see cref="AuthenticationTagMismatchException"/> if the key is wrong, the ciphertext was
    /// altered in transit, or the envelope it arrived in does not match the one it was sealed for.
    /// </summary>
    /// <param name="key">The shared key from <see cref="SessionKeyExchange.DeriveSharedKey"/>.</param>
    /// <param name="payload">The sealed payload taken off the wire.</param>
    /// <param name="associatedData">
    /// Recomputed from the received envelope. If a relay re-framed the payload — changed its type or
    /// re-emitted it under another session code — this will not match what was sealed and the tag
    /// check fails.
    /// </param>
    public static byte[] Open(byte[] key, SealedPayload payload, byte[] associatedData)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(associatedData);
        RequireDocumentedKeySize(key);

        if (payload.Ciphertext.Length < TagSize)
        {
            throw new CryptographicException("Payload is shorter than its authentication tag.");
        }

        var bodyLength = payload.Ciphertext.Length - TagSize;
        var body = payload.Ciphertext.AsSpan(0, bodyLength);
        var tag = payload.Ciphertext.AsSpan(bodyLength, TagSize);
        var plaintext = new byte[bodyLength];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(payload.Nonce, body, tag, plaintext, associatedData);

        return plaintext;
    }

    // AesGcm picks its algorithm from the length of the key it is handed: 16 bytes gives AES-128 and
    // 24 gives AES-192, with no error and no warning, because both are valid AES keys. This type
    // documents AES-256, so anything but KeySize is a caller's bug and must surface as one rather
    // than as a quietly weaker cipher. Do not soften this into padding or truncation.
    private static void RequireDocumentedKeySize(byte[] key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException(
                $"Key must be {KeySize} bytes for AES-256; got {key.Length}.",
                nameof(key));
        }
    }
}
