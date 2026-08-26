using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>
/// The relay's routing rules: given an envelope and the connection it arrived on, decide where it
/// goes. This is the whole of what the relay does with a message.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately transport-agnostic. It takes a parsed <see cref="WireEnvelope"/> and a connection
/// id, and returns a <see cref="RelayDecision"/>; it opens nothing, writes nothing and awaits
/// nothing. The rules are therefore testable without a socket, and the WebSocket adapter can be
/// replaced without reopening them.
/// </para>
/// <para>
/// What the relay can read is fixed by the wire format and not by policy: <c>Type</c> and
/// <c>SessionCode</c> travel in the clear because routing is impossible otherwise, and a
/// <see cref="WireMessageType.SessionPayload"/> is ciphertext it forwards without opening
/// (R-1.9, A-1.5f, D-11). Nothing in this type touches <see cref="SessionCipher"/>, and the relay
/// assembly holds no key material at all.
/// </para>
/// </remarks>
public sealed class RelayRouter(SessionRegistry registry)
{
    private readonly SessionRegistry _registry = registry;

    /// <summary>Decides what happens to one received envelope.</summary>
    /// <param name="envelope">The parsed message.</param>
    /// <param name="senderConnectionId">The connection it arrived on.</param>
    public RelayDecision Route(WireEnvelope envelope, string senderConnectionId)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrEmpty(senderConnectionId);

        // Unreachable from a real receive path today, and kept deliberately rather than by
        // oversight: EnvelopeCodec refuses to decode an envelope whose code does not parse, so
        // nothing arriving over a socket gets here with a bad one. The router still has to turn a
        // string into a SessionCode, and refusing is the only alternative to throwing inside the
        // one method every message passes through. It is a conversion, not a duplicated check.
        if (!SessionCode.TryParse(envelope.SessionCode, out var code))
        {
            return RelayDecision.Drop(RelayOutcome.MalformedSessionCode);
        }

        return envelope.Type switch
        {
            WireMessageType.CodeRequest => Arbitrate(code, senderConnectionId),
            WireMessageType.JoinRequest => RouteJoinRequest(code, senderConnectionId, envelope.PublicKey),
            WireMessageType.SessionPayload => ForwardPayload(code, senderConnectionId),

            // The host's answer to a join request. The relay reads who it is addressed to, opens the
            // gate or closes it, and forwards the host's own bytes unchanged — it composes nothing,
            // so nothing it authors can reach a participant (D-3).
            WireMessageType.JoinAccepted => RouteAdmission(envelope, code, senderConnectionId, admit: true),
            WireMessageType.JoinDenied or WireMessageType.JoinLapsed =>
                RouteAdmission(envelope, code, senderConnectionId, admit: false),

            // CodeAccepted and CodeRefused are the relay's own answers. A client sending one is not
            // a case the plugin can produce, so it is something hand-rolled talking to us, and the
            // relay must not launder it onward as though it had arbitrated.
            WireMessageType.CodeAccepted or WireMessageType.CodeRefused =>
                RelayDecision.Drop(RelayOutcome.RelayOnlyMessageFromClient),

            // D-14: the wire format only grows, so an unknown type is a message from a newer
            // client and not a fault. Ignore it and keep the connection — refusing or closing here
            // would make every additive change a breaking one, which is the whole thing D-14 exists
            // to prevent. This is the single place the relay decides that, rather than a judgement
            // each future handler would have to remember to repeat.
            // D-14: EnvelopeCodec maps any type this build does not know to Unknown, so an unknown
            // type is a message from a newer client and not a fault. Ignore it and keep the
            // connection — refusing here would make every additive change a breaking one, which is
            // the whole thing D-14 exists to prevent.
            WireMessageType.Unknown or _ => RelayDecision.Drop(RelayOutcome.UnrecognisedMessageType),
        };
    }

    /// <summary>
    /// The namespace arbitration R-1.2a places here: the host proposes a code, the relay accepts it
    /// or refuses it as already live, and the host regenerates and retries on a refusal.
    /// </summary>
    private RelayDecision Arbitrate(SessionCode code, string hostConnectionId) =>
        _registry.TryClaim(code, hostConnectionId)
            ? RelayDecision.Respond(RelayOutcome.CodeClaimed, WireEnvelope.ForCodeAccepted(code))
            : RelayDecision.Respond(RelayOutcome.CodeAlreadyLive, WireEnvelope.ForCodeRefused(code));

    /// <summary>
    /// Sends a join request to the host of that session and to nobody else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Narrower than "forward to session members", deliberately: the joiner's ephemeral public key
    /// and the bare fact that someone is trying to join stay off every other member's wire. Only
    /// the DM decides who is at the table (D-3), so the host is the only party that needs it.
    /// </para>
    /// <para>
    /// A joiner naming a code no session is live under is answered with
    /// <see cref="WireMessageType.CodeRefused"/>. That is the only "no" this protocol has — there is
    /// no session-not-found message — and R-1.8 requires the plugin to distinguish "that session
    /// code is not active" from a broken connection rather than showing a spinner, which it cannot
    /// do if the relay stays silent. A client can tell the two meanings apart because it knows what
    /// it sent: after a code request a refusal means regenerate, after a join request it means that
    /// session is not live. Recorded as a protocol gap in this chunk's PR rather than papered over.
    /// </para>
    /// </remarks>
    private RelayDecision RouteJoinRequest(SessionCode code, string joinerConnectionId, byte[]? envelopePublicKey)
    {
        if (!_registry.TryGetHost(code.Value, out var hostConnectionId))
        {
            return RelayDecision.Respond(RelayOutcome.SessionNotFound, WireEnvelope.ForCodeRefused(code));
        }

        if (string.Equals(hostConnectionId, joinerConnectionId, StringComparison.Ordinal))
        {
            return RelayDecision.Drop(RelayOutcome.RelayOnlyMessageFromClient);
        }

        if (envelopePublicKey is null)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        // Pending, NOT routed. R-1.3b: not admitted, not routed — a connection waiting on the DM
        // receives no session traffic at all, including ciphertext it could not read, because a
        // count and a cadence are inference D-13 forbids just as much as readable content is.
        _registry.TryRegisterPending(code.Value, joinerConnectionId, envelopePublicKey);

        return RelayDecision.Forward(RelayOutcome.JoinForwardedToHost, [hostConnectionId]);
    }

    /// <summary>
    /// Carries the host's admission decision to the joiner it names, and opens or closes the gate
    /// accordingly (R-1.3b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the host may decide. A decision from any other connection is refused rather than obeyed
    /// — the DM's accept/deny is the entire trust model, so a relay acting on somebody else's
    /// admission would be the one place that model could be bypassed.
    /// </para>
    /// <para>
    /// The joiner is named by <see cref="WireEnvelope.PublicKey"/>, which always means the joiner's
    /// key and never anything else (D-14). The relay needs no identifier of its own for a
    /// participant, and must not mint one.
    /// </para>
    /// </remarks>
    private RelayDecision RouteAdmission(WireEnvelope envelope, SessionCode code, string senderConnectionId, bool admit)
    {
        if (!_registry.TryGetHost(code.Value, out var hostConnectionId))
        {
            return RelayDecision.Drop(RelayOutcome.SessionNotFound);
        }

        if (!string.Equals(hostConnectionId, senderConnectionId, StringComparison.Ordinal))
        {
            return RelayDecision.Drop(RelayOutcome.AdmissionFromNonHost);
        }

        if (envelope.PublicKey is null)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        if (admit)
        {
            return _registry.TryAdmit(code.Value, envelope.PublicKey, out var admitted)
                ? RelayDecision.Forward(RelayOutcome.JoinerAdmitted, [admitted])
                : RelayDecision.Drop(RelayOutcome.UnknownJoiner);
        }

        return _registry.TryDeny(code.Value, envelope.PublicKey, out var rejected)
            ? RelayDecision.Forward(RelayOutcome.JoinerRejected, [rejected], closeAfterwards: true)
            : RelayDecision.Drop(RelayOutcome.UnknownJoiner);
    }

    /// <summary>
    /// Passes an encrypted payload to the other members of its session, unopened and unmodified.
    /// </summary>
    private RelayDecision ForwardPayload(SessionCode code, string senderConnectionId)
    {
        if (!_registry.IsParticipant(code.Value, senderConnectionId))
        {
            return RelayDecision.Drop(RelayOutcome.SenderNotInSession);
        }

        // Pending is not admitted, and an unadmitted connection neither receives session traffic
        // nor originates it (R-1.3b).
        if (!_registry.IsMember(code.Value, senderConnectionId))
        {
            return RelayDecision.Drop(RelayOutcome.SenderNotAdmitted);
        }

        return RelayDecision.Forward(
            RelayOutcome.PayloadForwarded,
            _registry.MembersExcept(code.Value, senderConnectionId));
    }
}
