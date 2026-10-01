using System;

namespace DungeonMasterXIV.Net;

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
