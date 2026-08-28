using System;
using System.Linq;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The host telling the session who is in it (R-1.3f), sealed (D-11).
/// </summary>
/// <remarks>
/// <para>
/// <b>The host authors and every player renders (D-3).</b> Nothing here reads a roster from anyone
/// else, and no player client originates one. That is the whole of the access model: a participant
/// is Owner of their own entry because they chose the name in it, Observer on everyone else's, and a
/// client that is not admitted is at None — <b>absent from what is sent, not filtered on arrival</b>.
/// </para>
/// <para>
/// <b>A-1.14 is enforced by the relay, not by this type, and that is the point.</b>
/// <c>RelayRouter.ForwardPayload</c> drops a payload from a connection that is not a member and
/// forwards only to <c>MembersExcept</c>, so an unadmitted client is never a recipient. Filtering
/// here as well would look like defence in depth and would actually be a second place to get it
/// wrong; sending to the room and letting the gate hold is what makes the guarantee testable over
/// what a client RECEIVES.
/// </para>
/// <para>
/// <b>Sealed once per participant, because keys are pairwise.</b> <c>DeriveSharedKey</c> gives the
/// host a different shared secret with each peer, so there is no single key that reaches the room.
/// One payload per recipient is therefore the only option the current design offers. The cost is
/// real and worth naming: the relay forwards each to every other member too, who cannot open it and
/// discard it, so a push to N players is N sends and N(N-1) deliveries. At Tier 0 party sizes that
/// is nothing; a session-wide content key would fix it and is a crypto design decision, not a
/// refactor.
/// </para>
/// </remarks>
internal sealed class RosterBroadcast
{
    private readonly RelayLink _link;
    private readonly SessionAudience _audience;
    private readonly Func<SessionKeyExchange?> _hostKeys;
    private readonly Func<SessionCode?> _hostCode;

    /// <summary>Wires the broadcast to the host state it reads and the link it sends down.</summary>
    /// <param name="link">The connection. Sends; decides nothing.</param>
    /// <param name="audience">Who is admitted, and the keys to reach them.</param>
    /// <param name="hostKeys">The host's ephemeral keys, read at send time rather than captured.</param>
    /// <param name="hostCode">The session being hosted, or null when not hosting.</param>
    public RosterBroadcast(
        RelayLink link,
        SessionAudience audience,
        Func<SessionKeyExchange?> hostKeys,
        Func<SessionCode?> hostCode)
    {
        _link = link;
        _audience = audience;
        _hostKeys = hostKeys;
        _hostCode = hostCode;
    }

    /// <summary>
    /// Sends the current roster to everyone admitted. Called whenever it could have changed, and
    /// whenever somebody needs it again.
    /// </summary>
    /// <remarks>
    /// <b>Driven by the admission, not by the roster having changed</b>, which is what A-1.13a needs:
    /// a client reconnecting mid-session is re-admitted and must be sent the CURRENT roster, even
    /// though from the host's side nothing about the membership is new. A push that fired only on
    /// change would leave exactly that client looking at an empty list.
    /// </remarks>
    public void Publish()
    {
        if (_hostKeys() is not { } keys || _hostCode() is not { } code || !_link.IsReadyToSend)
        {
            return;
        }

        var roster = _audience.Recipients
            .Select(peer => new RosterEntry(peer.PeerCode, peer.DisplayName.Value, peer.Role))
            .ToList();

        if (roster.Count == 0)
        {
            return;
        }

        var plaintext = SessionContentCodec.Encode(new SessionContent { Roster = roster });
        var associatedData = WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload);

        foreach (var peer in _audience.Recipients)
        {
            // A peer with no key is one the host cannot speak to. It cannot arise on the production
            // path — AdmissionControl takes the key off the request it is answering — so this is not
            // a fallback, it is the guard that keeps a future caller from creating a participant who
            // is addressable and unreachable without anyone noticing.
            if (peer.PublicKey is not { } peerKey)
            {
                continue;
            }

            byte[] shared;
            try
            {
                shared = keys.DeriveSharedKey(peerKey, code);
            }
            catch (CryptographicException)
            {
                // A key that will not import. NOTHING VALIDATES THAT A JOINER'S PUBLIC KEY IS A
                // WELL-FORMED SPKI BLOB — it arrives on the wire and is carried to admission — so a
                // client sending three arbitrary bytes reaches here. Skipping that peer keeps the
                // session serving everyone else; throwing would let any joiner crash the DM's
                // admission by sending rubbish, which is a denial of service through the one path
                // that is open to strangers by design.
                //
                // The peer stays admitted and simply hears nothing, which is the honest outcome for
                // a participant the host cannot address. Refusing such an admission outright is a
                // product decision about what the DM is told, not one to take here.
                continue;
            }

            var sealedPayload = SessionCipher.Seal(shared, plaintext, associatedData);
            _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
        }
    }
}
