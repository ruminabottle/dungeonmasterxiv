using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Turns a <see cref="SessionContent"/> into the bytes that go inside the seal, and back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separate from <see cref="EnvelopeCodec"/> on purpose.</b> That one encodes what the relay
/// reads in order to route; this encodes what only session members can read. Two codecs make the
/// boundary a type rather than a convention — a field added to the wrong one is a visible mistake
/// instead of a quiet disclosure, which is the class of error that put a display name in the clear.
/// </para>
/// <para>
/// <b>Decoding never throws and never trusts.</b> These bytes arrive from another client, and while
/// the seal proves they came from someone holding the shared key, it does not make their contents
/// well-formed. Malformed content is refused the way a malformed envelope is: a false return and no
/// exception, because a participant sending nonsense must not be able to end anyone's session.
/// </para>
/// </remarks>
public static class SessionContentCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serialises <paramref name="content"/> for sealing.</summary>
    /// <param name="content">The document to encode.</param>
    public static byte[] Encode(SessionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return JsonSerializer.SerializeToUtf8Bytes(content, Options);
    }

    /// <summary>
    /// Reads content that has already been opened, or reports that it is not content.
    /// </summary>
    /// <param name="plaintext">Bytes returned by <see cref="SessionCipher.Open"/>.</param>
    /// <param name="content">The decoded document, or null.</param>
    /// <returns>Whether the bytes were a document this build understands.</returns>
    public static bool TryDecode(byte[] plaintext, out SessionContent? content)
    {
        content = null;

        if (plaintext is null || plaintext.Length == 0)
        {
            return false;
        }

        try
        {
            content = JsonSerializer.Deserialize<SessionContent>(plaintext, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        return content is not null;
    }
}
