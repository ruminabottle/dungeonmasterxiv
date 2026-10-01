using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Routes one decoded inbound envelope to the matching handler for a join attempt or hosted session.</summary>
internal readonly record struct InboundFrame(
    JoinAttempt Attempt,
    SessionKeyExchange? Keys,
    HostSession? Host,
    InboundHandlers Handlers,
    ISessionTransportLog? Log)
{
    internal byte[]? Apply(WireEnvelope envelope, byte[]? sessionKey)
    {
        var host = Host;

        if (host is not null && InboundApplication.ApplyRegistration(envelope, host))
        {
            return sessionKey;
        }

        if (TryContent(envelope, sessionKey)
            || TryJoinHello(envelope)
            || TryJoinRequest(envelope)
            || TryConnectionDropped(envelope)
            || TryHostKey(envelope)
            || TryCodeRefused(envelope)
            || TryPendingNotice(envelope))
        {
            return sessionKey;
        }

        return ApplyOutcome(envelope, sessionKey);
    }

    private bool TryContent(WireEnvelope envelope, byte[]? sessionKey)
    {
        var handlers = Handlers;
        var log = Log;

        if (envelope.Type == WireMessageType.SessionPayload)
        {
            InboundApplication.ApplyContent(envelope, sessionKey ?? handlers.HostAuthored.OpenWith, handlers.HostAuthored.OnContent, log);

            MemberContentReader.Apply(envelope, handlers, log);
            return true;
        }

        return false;
    }

    private bool TryJoinHello(WireEnvelope envelope)
    {
        if (envelope.Type != WireMessageType.JoinHello)
        {
            return false;
        }

        if (Handlers.Admission.OnHello is { } onHello
            && envelope.PublicKey is { } joinerPublicKey
            && SessionKeyExchange.CanAgreeWith(joinerPublicKey))
        {
            onHello(joinerPublicKey);
        }

        return true;
    }

    private bool TryJoinRequest(WireEnvelope envelope)
    {
        if (envelope.Type != WireMessageType.JoinRequest)
        {
            return false;
        }

        if (Handlers.Admission.OnJoinRequest is { } onJoinRequest
            && envelope.PublicKey is { } joinerPublicKey
            && SessionKeyExchange.CanAgreeWith(joinerPublicKey))
        {
            onJoinRequest(joinerPublicKey, envelope);
        }

        return true;
    }

    private bool TryConnectionDropped(WireEnvelope envelope)
    {
        var handlers = Handlers;

        if (envelope.Type == WireMessageType.ConnectionDropped)
        {
            handlers.Transport.Deliver(envelope);
            return true;
        }

        return false;
    }

    private bool TryHostKey(WireEnvelope envelope)
    {
        if (envelope.Type != WireMessageType.HostKey)
        {
            return false;
        }

        var attempt = Attempt;

        if (Keys is { } keys
            && envelope.PublicKey is { } addressee
            && CryptographicOperations.FixedTimeEquals(addressee, keys.PublicKey)
            && envelope.HostPublicKey is { } hostPublicKey)
        {
            if (SessionKeyExchange.CanAgreeWith(hostPublicKey))
            {
                attempt.HostKeyOffered(hostPublicKey);
            }
            else if (attempt.Phase == JoinPhase.Contacting)
            {
                attempt.Fail(SessionFailure.HostKeyUnusable);
            }
        }

        return true;
    }

    private bool TryCodeRefused(WireEnvelope envelope)
    {
        var attempt = Attempt;

        if (envelope.Type == WireMessageType.CodeRefused && attempt.Phase == JoinPhase.Contacting)
        {
            attempt.Fail(SessionFailure.SessionCodeNotActive);
            return true;
        }

        return false;
    }

    private bool TryPendingNotice(WireEnvelope envelope)
    {
        if (envelope.TryGetPendingHostKey() is null)
        {
            return false;
        }

        Attempt.AwaitDecision(envelope.TryGetDeadline());
        return true;
    }

    private byte[]? ApplyOutcome(WireEnvelope envelope, byte[]? sessionKey)
    {
        var attempt = Attempt;
        var keys = Keys;

        if (envelope.TryGetAdmissionOutcome(keys?.PublicKey) is { } outcome)
        {
            sessionKey = InboundApplication.Apply(
                outcome, attempt, keys, ParticipantReceipt.TryOpen(envelope, keys, attempt.Code)) ?? sessionKey;
        }
        else if (envelope.TryReadAdmissionAnswer() is not null)
        {
            Log?.Warning(
                "An admission answer arrived addressed to a different client and was discarded. "
                + "These are sent to one recipient, so this is a routing fault or traffic from "
                + "something other than this plugin rather than normal cross-talk. This client's "
                + "own join was not affected.");
        }

        return sessionKey;
    }
}
