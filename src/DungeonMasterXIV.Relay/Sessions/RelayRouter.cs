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
            WireMessageType.CodeRequest => Arbitrate(code, senderConnectionId, envelope.ReclaimHash),
            WireMessageType.JoinHello => RouteJoinHello(code, senderConnectionId, envelope.PublicKey),
            WireMessageType.JoinRequest => RouteJoinRequest(code, senderConnectionId, envelope.PublicKey),
            WireMessageType.HostKey =>
                RouteToPendingJoiner(envelope, code, senderConnectionId, RelayOutcome.HostKeyForwarded),
            WireMessageType.SessionPayload => ForwardPayload(code, senderConnectionId),

            WireMessageType.JoinAccepted => RouteAdmission(envelope, code, senderConnectionId, admit: true),
            WireMessageType.JoinDenied or WireMessageType.JoinLapsed =>
                RouteAdmission(envelope, code, senderConnectionId, admit: false),

            WireMessageType.JoinPending =>
                RouteToPendingJoiner(envelope, code, senderConnectionId, RelayOutcome.PendingNoticeForwarded),

            WireMessageType.Reclaim => RouteReclaim(envelope, code, senderConnectionId),
            WireMessageType.Resume => RouteResume(code, senderConnectionId, envelope.PublicKey),

            WireMessageType.HostAway or WireMessageType.HostBack or WireMessageType.Reclaimed =>
                RelayDecision.Drop(RelayOutcome.RelayOnlyMessageFromClient),

            WireMessageType.CodeAccepted or WireMessageType.CodeRefused or WireMessageType.ConnectionDropped =>
                RelayDecision.Drop(RelayOutcome.RelayOnlyMessageFromClient),

            WireMessageType.Unknown or _ => RelayDecision.Drop(RelayOutcome.UnrecognisedMessageType),
        };
    }

    private RelayDecision Arbitrate(SessionCode code, string hostConnectionId, byte[]? reclaimHash) =>
        _registry.TryClaim(code, hostConnectionId, reclaimHash)
            ? RelayDecision.Respond(RelayOutcome.CodeClaimed, WireEnvelope.ForCodeAccepted(code))
            : RelayDecision.Respond(RelayOutcome.CodeAlreadyLive, WireEnvelope.ForCodeRefused(code));

    private RelayDecision RouteJoinHello(SessionCode code, string joinerConnectionId, byte[]? envelopePublicKey)
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

    private RelayDecision RouteJoinRequest(SessionCode code, string joinerConnectionId, byte[]? envelopePublicKey)
    {
        if (!_registry.TryGetHost(code.Value, out var hostConnectionId))
        {
            return RelayDecision.Respond(RelayOutcome.SessionNotFound, WireEnvelope.ForCodeRefused(code));
        }

        if (envelopePublicKey is null)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        if (!_registry.TryGetPending(code.Value, envelopePublicKey, out var waiting)
            || !string.Equals(waiting, joinerConnectionId, StringComparison.Ordinal))
        {
            return RelayDecision.Drop(RelayOutcome.UnknownJoiner);
        }

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

    private RelayDecision RouteToPendingJoiner(
        WireEnvelope envelope,
        SessionCode code,
        string senderConnectionId,
        RelayOutcome forwarded)
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
            ? RelayDecision.Forward(forwarded, [waiting])
            : RelayDecision.Drop(RelayOutcome.UnknownJoiner);
    }

    private RelayDecision RouteReclaim(WireEnvelope envelope, SessionCode code, string senderConnectionId)
    {
        if (envelope.ReclaimSecret is not { } secret)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        return _registry.TryReclaim(code, secret, senderConnectionId, out var members)
            ? RelayDecision.Respond(RelayOutcome.Reclaimed, WireEnvelope.ForReclaimed(code))
                .AlsoTelling(members, WireEnvelope.ForHostBack(code))
            : RelayDecision.Respond(RelayOutcome.ReclaimRefused, WireEnvelope.ForCodeRefused(code));
    }

    private RelayDecision RouteResume(SessionCode code, string memberConnectionId, byte[]? publicKey)
    {
        if (_registry.IsHostAway(code.Value))
        {
            return RelayDecision.Respond(RelayOutcome.HostAway, WireEnvelope.ForHostAway(code));
        }

        if (!_registry.TryGetHost(code.Value, out var hostConnectionId))
        {
            return RelayDecision.Respond(RelayOutcome.SessionNotFound, WireEnvelope.ForCodeRefused(code));
        }

        if (publicKey is null)
        {
            return RelayDecision.Drop(RelayOutcome.MalformedEnvelope);
        }

        _registry.TryRegisterPending(code.Value, memberConnectionId, publicKey);
        return RelayDecision.Forward(RelayOutcome.ResumeForwarded, [hostConnectionId]);
    }

    private RelayDecision ForwardPayload(SessionCode code, string senderConnectionId)
    {
        if (_registry.IsHostAway(code.Value))
        {
            return RelayDecision.Drop(RelayOutcome.HostAway);
        }

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
