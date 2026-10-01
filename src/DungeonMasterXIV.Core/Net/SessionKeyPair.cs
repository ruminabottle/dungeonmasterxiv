using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Makes a session key pair, returning false instead of throwing if key creation raises a crypto error.</summary>
public static class SessionKeyPair
{
    public static bool TryMake(Func<SessionKeyExchange> newKeys, out SessionKeyExchange? keys)
    {
        ArgumentNullException.ThrowIfNull(newKeys);

        try
        {
            keys = newKeys();
            return true;
        }
        catch (CryptographicException)
        {
            keys = null;
            return false;
        }
    }
}
