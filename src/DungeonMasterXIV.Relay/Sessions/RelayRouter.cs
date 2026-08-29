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

            // The host's key on its way to a joiner who is still waiting (R-1.3a-i). Its own arm and
            // NOT RouteAdmission with a flag: that method resolves the pending entry as well as
            // forwarding, so reusing it with admit:false would deliver this notice and deny the
            // joiner in the same call. A message that answers nothing must move no gate.
            WireMessageType.JoinPending => RouteJoinPending(envelope, code, senderConnectionId),

            // The joiner telling the host it holds the host's key and has a fingerprint to read
            // (R-1.3a-iii). Travels joiner -> host like a join request, and moves no gate: the
            // joiner is already pending and this answers nothing.
            WireMessageType.JoinerHoldsFingerprint =>
                RouteFingerprintReceipt(envelope, code, senderConnectionId),

            // The relay's OWN messages -- a client sending one is something hand-rolled talking to
            // us, and must not be laundered onward as though the relay had arbitrated. See
            // WireMessageType.ConnectionDropped for what forwarding that one would buy an attacker.
            WireMessageType.CodeAccepted
                or WireMessageType.CodeRefused
                or WireMessageType.ConnectionDropped =>
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
    /// Carries the host's public key to a joiner that is still waiting, so it can compare the
    /// fingerprint before the DM decides (R-1.3a-i, A-1.3f-1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Host-only, and that check is load-bearing rather than symmetry with
    /// <see cref="RouteAdmission"/>.</b> This message is how a joiner learns which key to expect. A
    /// relay that forwarded it from any connection would let a third party post a key of their own
    /// choosing to the joiner, who would then compare the fingerprint of the substituted key and
    /// find it matches — inverting the defence rather than weakening it. Refused, not obeyed.
    /// </para>
    /// <para>
    /// <b>The gate is not touched.</b> The joiner stays pending: nobody has decided anything, and a
    /// notice that quietly admitted or dropped its recipient would make "the DM is looking at your
    /// request" into an answer (R-1.3b).
    /// </para>
    /// </remarks>
    private RelayDecision RouteJoinPending(WireEnvelope envelope, SessionCode code, string senderConnectionId)
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

        return _registry.TryGetPending(code.Value, envelope.PublicKey, out var waiting)
            ? RelayDecision.Forward(RelayOutcome.PendingNoticeForwarded, [waiting])
            : RelayDecision.Drop(RelayOutcome.UnknownJoiner);
    }

    /// <summary>
    /// Carries a joiner's report that it can render a fingerprint to the host of that session
    /// (R-1.3a-iii), and to nobody else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This arm exists because its absence is invisible.</b> Every part of this receipt was built
    /// and tested on the client — the joiner sends it, the host has somewhere to put it — and the
    /// relay in between would have dropped it to
    /// <see cref="RelayOutcome.UnrecognisedMessageType"/> under D-14's catch-all, which is correct
    /// behaviour for a message from the future and silent ruin for one from the present. That is
    /// exactly what happened to <see cref="WireMessageType.JoinPending"/> and became BUG-33.
    /// </para>
    /// <para>
    /// <b>Narrowed to the host, like a join request.</b> Who is trying to join and what their client
    /// can do stays off every other member's wire (D-3, D-8); only the DM decides who is at the
    /// table, so only the DM needs it.
    /// </para>
    /// <para>
    /// <b>It moves no gate.</b> The joiner is already pending and this reports a capability rather
    /// than answering anything — the same reason <see cref="RouteJoinPending"/> is its own arm
    /// rather than <see cref="RouteAdmission"/> with a flag.
    /// </para>
    /// </remarks>
    private RelayDecision RouteFingerprintReceipt(
        WireEnvelope envelope,
        SessionCode code,
        string senderConnectionId)
    {
        if (!_registry.TryGetHost(code.Value, out var hostConnectionId))
        {
            return RelayDecision.Drop(RelayOutcome.SessionNotFound);
        }

        // A host cannot report holding its own key. Anything sending this from the host's connection
        // is not the plugin, and the relay does not launder it onward.
        if (string.Equals(hostConnectionId, senderConnectionId, StringComparison.Ordinal))
        {
            return RelayDecision.Drop(RelayOutcome.RelayOnlyMessageFromClient);
        }

        if (envelope.PublicKey is null)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        return RelayDecision.Forward(RelayOutcome.JoinForwardedToHost, [hostConnectionId]);
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
