using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>Routes each envelope by type, updating the session registry and returning what the relay should do.</summary>
public sealed class RelayRouter(SessionRegistry registry)
{
    private readonly SessionRegistry _registry = registry;

    public RelayDecision Route(WireEnvelope envelope, string senderConnectionId)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrEmpty(senderConnectionId);

        if (!SessionCode.TryParse(envelope.SessionCode, out var code))
        {
            return RelayDecision.Drop(RelayOutcome.MalformedSessionCode);
        }

        return envelope.Type switch
        {
            WireMessageType.CodeRequest => Arbitrate(code, senderConnectionId),
            WireMessageType.JoinRequest => RouteJoinRequest(code, senderConnectionId, envelope.PublicKey),
            WireMessageType.SessionPayload => ForwardPayload(code, senderConnectionId),

            WireMessageType.JoinAccepted => RouteAdmission(envelope, code, senderConnectionId, admit: true),
            WireMessageType.JoinDenied or WireMessageType.JoinLapsed =>
                RouteAdmission(envelope, code, senderConnectionId, admit: false),

            WireMessageType.JoinPending => RouteJoinPending(envelope, code, senderConnectionId),

            WireMessageType.JoinerHoldsFingerprint =>
                RouteFingerprintReceipt(envelope, code, senderConnectionId),

            WireMessageType.CodeAccepted or WireMessageType.CodeRefused or WireMessageType.ConnectionDropped =>
                RelayDecision.Drop(RelayOutcome.RelayOnlyMessageFromClient),

            WireMessageType.Unknown or _ => RelayDecision.Drop(RelayOutcome.UnrecognisedMessageType),
        };
    }

    private RelayDecision Arbitrate(SessionCode code, string hostConnectionId) =>
        _registry.TryClaim(code, hostConnectionId)
            ? RelayDecision.Respond(RelayOutcome.CodeClaimed, WireEnvelope.ForCodeAccepted(code))
            : RelayDecision.Respond(RelayOutcome.CodeAlreadyLive, WireEnvelope.ForCodeRefused(code));

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

        _registry.TryRegisterPending(code.Value, joinerConnectionId, envelopePublicKey);

        return RelayDecision.Forward(RelayOutcome.JoinForwardedToHost, [hostConnectionId]);
    }

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

    private RelayDecision RouteFingerprintReceipt(
        WireEnvelope envelope,
        SessionCode code,
        string senderConnectionId)
    {
        if (!_registry.TryGetHost(code.Value, out var hostConnectionId))
        {
            return RelayDecision.Drop(RelayOutcome.SessionNotFound);
        }

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

    private RelayDecision ForwardPayload(SessionCode code, string senderConnectionId)
    {
        if (!_registry.IsParticipant(code.Value, senderConnectionId))
        {
            return RelayDecision.Drop(RelayOutcome.SenderNotInSession);
        }

        if (!_registry.IsMember(code.Value, senderConnectionId))
        {
            return RelayDecision.Drop(RelayOutcome.SenderNotAdmitted);
        }

        return RelayDecision.Forward(
            RelayOutcome.PayloadForwarded,
            _registry.MembersExcept(code.Value, senderConnectionId));
    }
}
