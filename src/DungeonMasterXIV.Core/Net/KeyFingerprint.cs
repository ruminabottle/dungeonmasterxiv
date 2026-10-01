using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

public static class KeyFingerprint
{
    public const int Characters = 11;

    public static string Of(byte[] oneKey, byte[] otherKey)
    {
        ArgumentNullException.ThrowIfNull(oneKey);
        ArgumentNullException.ThrowIfNull(otherKey);

        var (low, high) = oneKey.AsSpan().SequenceCompareTo(otherKey) <= 0
            ? (oneKey, otherKey)
            : (otherKey, oneKey);

        var digest = SHA256.HashData(LengthPrefixed(low, high));
        var value = new BigInteger(digest, isUnsigned: true, isBigEndian: true);

        var rendered = new char[Characters];
        for (var i = Characters - 1; i >= 0; i--)
        {
            value = BigInteger.DivRem(value, SpeakableAlphabet.Length, out var symbol);
            rendered[i] = SpeakableAlphabet.Characters[(int)symbol];
        }

        return SpeakableAlphabet.Group(new string(rendered));
    }

    private static byte[] LengthPrefixed(byte[] low, byte[] high)
    {
        var buffer = new byte[sizeof(int) + low.Length + sizeof(int) + high.Length];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt32BigEndian(span, low.Length);
        low.CopyTo(span[sizeof(int)..]);

        var afterLow = sizeof(int) + low.Length;
        BinaryPrimitives.WriteInt32BigEndian(span[afterLow..], high.Length);
        high.CopyTo(span[(afterLow + sizeof(int))..]);

        return buffer;
    }
}
