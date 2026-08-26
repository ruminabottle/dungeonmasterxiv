using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Produces session codes. Stateless and holds no history, deliberately.
/// </summary>
/// <remarks>
/// <para>
/// <b>This does not check uniqueness, and must not.</b> PRD-1 R-1.2a places collision resolution at
/// the relay: the relay routes by session code, so the namespace is relay-wide and a host cannot
/// know what is free. A generator that deduplicated against anything local would be checking the
/// wrong set — it would pass every local check and still collide on the relay.
/// </para>
/// <para>
/// The exchange that does resolve it — request, refusal, regenerate, retry — is carried by
/// <see cref="WireEnvelope"/>. Arbitrating it is the relay's job, not this type's.
/// </para>
/// </remarks>
public static class SessionCodeGenerator
{
    /// <summary>
    /// Returns a new code drawn uniformly from <see cref="SessionCode.Alphabet"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="RandomNumberGenerator.GetInt32(int)"/> rather than a byte modulo, which
    /// would bias the first 16 characters of a 24-symbol alphabet: 256 is not a multiple of 24.
    /// </remarks>
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
