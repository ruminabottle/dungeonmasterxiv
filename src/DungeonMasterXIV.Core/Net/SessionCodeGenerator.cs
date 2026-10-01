using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

public static class SessionCodeGenerator
{
    public static SessionCode Next()
    {
        var characters = new char[SessionCode.Length];
        for (var i = 0; i < characters.Length; i++)
        {
            characters[i] = SessionCode.Alphabet[RandomNumberGenerator.GetInt32(SessionCode.Alphabet.Length)];
        }

        return SessionCode.FromValid(new string(characters));
    }
}
