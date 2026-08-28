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
/// <b>Two fields of a roster entry are vetted here — <see cref="RosterEntry.DisplayName"/> and
/// <see cref="RosterEntry.PeerCode"/> — and naming them is the point.</b> The previous version of
/// this paragraph said every display name goes through <see cref="DisplayName"/> "and this is the
/// only door". That was narrowly true and read as a claim about the whole record, so a reviewer met
/// it and was told the boundary was closed. It was not: <c>PeerCode</c> passed through untouched,
/// and the forged <c>"Code to compare"</c> line the name gate exists to stop simply moved one field
/// over (BUG-57).
/// </para>
/// <para>
/// <b>Why that hole was not obvious, which is the part worth carrying.</b> A peer code is derived
/// host-side by <c>AdmissionControl.PeerCodeFor</c>, so within one client it has always been
/// trustworthy. <b>The roster is the first place a peer code arrives from ANOTHER client</b> — the
/// field did not change, the direction it travels did.
/// </para>
/// <para>
/// The seal authenticates the sender; it says nothing about what they sent. Both fields are
/// untrusted data rendered beside a fingerprint, so a multi-line one could draw a forged
/// <c>"Code to compare"</c> line — displacing the fingerprint, which the D-8 gate denies on sight.
/// Validating at the boundary rather than at each consumer is the difference between a rule and a
/// reminder: a later reader of a decoded <see cref="SessionContent"/> must not have to remember.
/// </para>
/// <para>
/// <b><see cref="RosterEntry.Role"/> is not vetted, and that is a finding rather than an omission —
/// an unmentioned field is what let <c>PeerCode</c> through.</b> It is an <c>enum</c> over
/// <c>int</c> with no string converter, and this was MEASURED rather than reasoned: a numeric value
/// out of range decodes to an undefined member — <c>99</c> and <c>int.MaxValue</c> both arrive, and
/// <c>ToString()</c> gives back the digits with no newline — while every STRING form, including
/// <c>"DungeonMaster\nCode to compare: FORGED"</c>, is refused at decode outright because the
/// deserialiser throws and <c>TryDecode</c> returns false. So Role cannot carry text and cannot
/// forge a line. What it can do is present a value matching no case, which is a rendering question
/// for whoever builds T-14 and not an injection one.
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
                Roster = [.. content.Roster
                    .Where(entry => IsAPeerCodeThisProductGenerates(entry.PeerCode))
                    .Select(entry => entry with
                    {
                        DisplayName = DisplayName.OrNone(entry.DisplayName).Value,
                    })],
            };

    /// <summary>
    /// Whether <paramref name="peerCode"/> is the shape <c>AdmissionControl.PeerCodeFor</c> actually
    /// produces: exactly <see cref="SessionCode.Length"/> characters, every one of them from
    /// <see cref="SpeakableAlphabet.Characters"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shape, not <see cref="SessionCode.TryParse"/>.</b> That method strips hyphens, trims
    /// and upper-cases so a pasted code works, which is right for something a human types and wrong
    /// here: it would accept <c>"PEE-R3"</c>, which <c>PeerCodeFor</c> never emits, and accepting a
    /// code the product cannot have generated is the thing this exists to stop. The length and the
    /// alphabet are taken from <see cref="SessionCode"/> rather than restated, so there is no second
    /// copy to drift.
    /// </para>
    /// <para>
    /// <b>A failure DROPS the entry, and that is deliberately the opposite of the name rule.</b> A
    /// display name is a label, so a refused one degrades to "a player who gave no name" and the
    /// participant stays. A peer code is the IDENTITY — it is what tells two participants with the
    /// same name apart (A-1.2d) — so an entry whose code is unusable identifies nobody, and
    /// degrading it would manufacture a participant rather than remove a forgery. The roster is
    /// host-authored and sealed, so a malformed code means our own encoder is broken or a keyholder
    /// is forging, and dropping is the safe answer to both.
    /// </para>
    /// </remarks>
    private static bool IsAPeerCodeThisProductGenerates(string? peerCode)
    {
        if (peerCode is null || peerCode.Length != SessionCode.Length)
        {
            return false;
        }

        foreach (var character in peerCode)
        {
            if (!SpeakableAlphabet.Characters.Contains(character))
            {
                return false;
            }
        }

        return true;
    }

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
