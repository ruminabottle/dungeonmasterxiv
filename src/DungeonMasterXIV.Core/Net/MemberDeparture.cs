using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A member telling the host it is leaving deliberately, so the host can remove it at once
/// (R-1.3g, A-1.16a).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE FIRST MEMBER-AUTHORED SEND IN THE PRODUCT.</b> Until this type,
/// <see cref="SessionCipher.Seal"/> had exactly one production caller — <c>RosterBroadcast</c>,
/// which is the host. <see cref="MemberContentReceipts"/> recorded that in its own doc and warned
/// that a reader who took the receiving half for a shipped behaviour had been misled. The receiving
/// half was real; nothing spoke into it.
/// </para>
/// <para>
/// <b>ONE ENVELOPE TO THE HOST, not a broadcast.</b> A member holds exactly one shared key — the one
/// derived with the host at admission — so it can seal for the host and for nobody else. That is
/// D-11's pairwise model doing the addressing: the relay forwards a member's payload to the other
/// members, and only the host can open this one.
/// </para>
/// <para>
/// <b>IT ASSERTS ONLY ITS OWN INTENT, WHICH IS WHY IT DOES NOT INVERT D-3.</b> The document says
/// <i>I am leaving</i> and nothing about the session. <b>The host decides what follows, and WHICH
/// member left is read from the key the payload opened under</b> — never from the payload — so a
/// member cannot remove anybody but itself.
/// </para>
/// <para>
/// <b>A QUIT IS NOT A VANISH (A-1.30).</b> This is the deliberate half: the host removes the seat at
/// once. A member that merely stops answering sends nothing, and the host records a drop while
/// HOLDING the seat (A-1.28). Nothing here is reachable by a silence, which is the property that
/// keeps the two apart.
/// </para>
/// </remarks>
internal sealed class MemberDeparture
{
    private readonly RelayLink _link;
    private readonly Func<SessionCode?> _code;
    private readonly Func<byte[]?> _sessionKey;

    /// <param name="link">The connection this client already holds.</param>
    /// <param name="code">The session being left, or null when not in one.</param>
    /// <param name="sessionKey">
    /// The key shared with the HOST, or null before admission. <b>Null is the ordinary answer for a
    /// client that never got in</b>, and it is why announcing is a no-op rather than a failure.
    /// </param>
    public MemberDeparture(RelayLink link, Func<SessionCode?> code, Func<byte[]?> sessionKey)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(sessionKey);

        _link = link;
        _code = code;
        _sessionKey = sessionKey;
    }

    /// <summary>
    /// Tells the host this client is leaving. Returns whether anything was sent.
    /// </summary>
    /// <remarks>
    /// <b>Silent and false when there is nothing to leave</b> — no code, or no shared key because
    /// this client was never admitted. A client that quits the join screen has nobody to tell, and
    /// treating that as an error would make the ordinary path noisy.
    /// </remarks>
    public bool Announce()
    {
        if (_code() is not { } code || _sessionKey() is not { } key)
        {
            return false;
        }

        var plaintext = SessionContentCodec.Encode(new SessionContent { Leaving = true });
        var sealedPayload = SessionCipher.Seal(
            key, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
        return true;
    }
}
