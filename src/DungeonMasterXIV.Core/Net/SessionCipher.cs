using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Seals and opens payloads with AES-256-GCM under a 32-byte session key.</summary>
public static class SessionCipher
{
    public const int KeySize = 32;

    public const int NonceSize = 12;

    public const int TagSize = 16;

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
