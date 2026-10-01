using System;

namespace DungeonMasterXIV.Net;

/// <summary>An encrypted payload: its nonce and its ciphertext with the authentication tag appended.</summary>
public sealed class SealedPayload
{
    internal SealedPayload(byte[] nonce, byte[] ciphertext)
    {
        Nonce = nonce;
        Ciphertext = ciphertext;
    }

    public byte[] Nonce { get; }

    public byte[] Ciphertext { get; }

    public static SealedPayload FromWire(byte[] nonce, byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(ciphertext);
        return new SealedPayload(nonce, ciphertext);
    }
}
