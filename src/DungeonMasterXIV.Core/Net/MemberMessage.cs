using System;
using DungeonMasterXIV.Chat;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A member saying something to the session, for the host to stamp and rebroadcast (R-2.19,
/// A-2.34).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PRODUCING HALF OF BASE CHAT, AND IT IS WHAT DMXENG-118 DID NOT SHIP.</b> That change put
/// <see cref="StreamLine"/> and <see cref="SessionContent.Entries"/> on the wire and gave them a
/// decode door, with <b>nothing at either end producing one</b> — measured at the time as zero
/// constructions of <c>StreamLine</c> anywhere in the tree. A type that can carry a message is not a
/// path by which a member can send one.
/// </para>
/// <para>
/// <b>ONE ENVELOPE TO THE HOST, not a broadcast</b>, exactly as <see cref="MemberDeparture"/> does
/// and for the same reason: a member holds one shared key, derived with the host at admission, so it
/// can seal for the host and for nobody else. D-11's pairwise model is the addressing.
/// </para>
/// <para>
/// <b>THE MEMBER SENDS TEXT AND NOTHING ELSE — no sequence, no stamp, no speaker.</b> Order is the
/// host's to mint (R-2.4) and identity is read from the key the payload opened under, so there is
/// nothing here for a member to forge. <b>That is why this sends <see cref="SessionContent.Saying"/>
/// rather than an <see cref="SessionContent.Entries"/> line</b>: a member-authored entry would carry
/// a sequence it had no authority to choose, and the decode door refuses it anyway.
/// </para>
/// <para>
/// <b>BOUNDED BEFORE IT IS SENT AND AGAIN WHEN IT ARRIVES.</b> This end refuses so the person who
/// typed it is told (A-2.35); the host end refuses because a peer is not obliged to run this code.
/// Neither check is redundant — they defend different things.
/// </para>
/// </remarks>
internal sealed class MemberMessage
{
    private readonly RelayLink _link;
    private readonly Func<SessionCode?> _code;
    private readonly Func<byte[]?> _sessionKey;

    /// <param name="link">The connection this client already holds.</param>
    /// <param name="code">The session being spoken in, or null when not in one.</param>
    /// <param name="sessionKey">
    /// The key shared with the HOST, or null before admission. <b>Null is the ordinary answer for a
    /// client that never got in</b>, which is why saying something is refused rather than thrown.
    /// </param>
    public MemberMessage(RelayLink link, Func<SessionCode?> code, Func<byte[]?> sessionKey)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(sessionKey);

        _link = link;
        _code = code;
        _sessionKey = sessionKey;
    }

    /// <summary>
    /// Sends <paramref name="text"/> to the host, or reports why it was refused.
    /// </summary>
    /// <remarks>
    /// <b>THE REFUSAL IS RETURNED RATHER THAN SWALLOWED, because A-2.35 is about the sender knowing.</b>
    /// A bool would collapse "too long", "nothing typed" and "you are not in a session" into one
    /// answer, and the criterion fails a build whose sender cannot tell what happened.
    /// </remarks>
    /// <param name="text">What the person typed.</param>
    /// <param name="limits">The bounds to apply.</param>
    /// <returns>The draft as composed — <see cref="MessageDraft.IsAccepted"/> false when nothing was sent.</returns>
    public MessageDraft Say(string? text, MessageLimits limits)
    {
        var draft = MessageDraft.Compose(text, limits);

        if (!draft.IsAccepted)
        {
            return draft;
        }

        if (_code() is not { } code || _sessionKey() is not { } key)
        {
            return new MessageDraft(null, MessageFault.NotInASession, "This client is not in a session.");
        }

        var plaintext = SessionContentCodec.Encode(new SessionContent { Saying = draft.Text });
        var sealedPayload = SessionCipher.Seal(
            key, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
        return draft;
    }
}
