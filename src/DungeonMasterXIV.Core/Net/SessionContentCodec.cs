using System;
using System.Linq;
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
/// <para>
/// <b>Every display name is put through <see cref="DisplayName"/> HERE, and this is the only door.</b>
/// The seal authenticates the sender; it says nothing about what they sent. A name is untrusted data
/// rendered beside a fingerprint, so a multi-line one could draw a forged <c>"Code to compare"</c>
/// line — a name displacing the fingerprint, which the D-8 gate denies on sight. Validating at the
/// boundary rather than at each consumer is the difference between a rule and a reminder: a later
/// reader of a decoded <see cref="SessionContent"/> must not have to remember.
/// </para>
/// </remarks>
public static class SessionContentCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The same document with every display name put through <see cref="DisplayName"/>.
    /// </summary>
    /// <remarks>
    /// A name the join path would refuse degrades to <see cref="DisplayName.Unstated"/> and
    /// <b>the participant stays in the roster</b> — exactly what the admission prompt already does.
    /// Dropping them would let a malformed name erase somebody from the session, which is a worse
    /// outcome than showing them unnamed.
    /// </remarks>
    private static SessionContent Vetted(SessionContent content) =>
        content.Roster is null
            ? content
            : new SessionContent
            {
                Roster = [.. content.Roster.Select(entry => entry with
                {
                    DisplayName = DisplayName.OrNone(entry.DisplayName).Value,
                })],
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

        if (content is null)
        {
            return false;
        }

        content = Vetted(content);
        return true;
    }
}
