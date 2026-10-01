using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Applies inbound envelopes: relay registration answers, sealed host content, and admission outcomes.</summary>
internal static class InboundApplication
{
    internal static bool ApplyRegistration(WireEnvelope envelope, HostSession host)
    {
        if (host.Phase != HostingPhase.Registering)
        {
            return false;
        }

        if (host.Code is not { } outstanding
            || !string.Equals(envelope.SessionCode, outstanding.Value, StringComparison.Ordinal))
        {
            return false;
        }

        switch (envelope.Type)
        {
            case WireMessageType.CodeAccepted:
                host.Registered();
                return true;

            case WireMessageType.CodeRefused:
                host.CodeAlreadyLive(SessionCodeGenerator.Next());
                return true;

            default:
                return false;
        }
    }

    internal static void ApplyContent(
        WireEnvelope envelope,
        byte[]? key,
        Action<SessionContent>? onContent,
        ISessionTransportLog? log)
    {
        if (onContent is null || key is null || envelope.TryGetSealedPayload() is not { } sealedPayload)
        {
            return;
        }

        byte[] plaintext;
        try
        {
            plaintext = SessionCipher.Open(key, sealedPayload, envelope.AssociatedData());
        }
        catch (CryptographicException)
        {
            return;
        }

        if (!SessionContentCodec.TryDecode(plaintext, out var content, log) || content is null)
        {
            log?.Warning(
                "A session payload authenticated and then failed to decode. It was sealed for this "
                + "client by a keyholder, so this is version skew or an encoding defect rather than "
                + "traffic for somebody else. The payload was discarded.");
            return;
        }

        onContent(content);
    }

    internal static byte[]? Apply(
        AdmissionOutcome outcome,
        JoinAttempt attempt,
        SessionKeyExchange? keys,
        Guid? participantId) =>
        outcome.Match(
            onAccepted: hostPublicKey =>
            {
                if (!SessionKeyExchange.CanAgreeWith(hostPublicKey))
                {
                    attempt.Fail(SessionFailure.HostKeyUnusable);
                    return null;
                }

                attempt.Admitted();

                if (participantId is { } told)
                {
                    attempt.ToldItIsParticipant(told);
                }

                return keys is not null && attempt.Code is { } code
                    ? keys.DeriveSharedKey(hostPublicKey, code)
                    : null;
            },
            onDenied: () =>
            {
                attempt.Denied();
                return (byte[]?)null;
            },
            onLapsed: () =>
            {
                attempt.Lapsed();
                return (byte[]?)null;
            });}
