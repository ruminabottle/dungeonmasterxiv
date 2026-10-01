using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Reads typed values from a wire envelope: host keys, deadlines, sealed payloads, admission answers.</summary>
public static class WireEnvelopeReading
{
    public static byte[]? TryGetPendingHostKey(this WireEnvelope envelope) =>
        envelope.Type == WireMessageType.JoinPending ? envelope.HostPublicKey : null;

    public static byte[]? TryGetFingerprintReceiptKey(this WireEnvelope envelope) =>
        envelope.Type == WireMessageType.JoinerHoldsFingerprint ? envelope.PublicKey : null;

    public static AdmissionOutcome? TryGetAdmissionOutcome(this WireEnvelope envelope, byte[]? ownPublicKey)
    {
        return envelope.TryReadAdmissionAnswer() is { } outcome && IsAddressedTo(envelope, ownPublicKey)
            ? outcome
            : null;
    }

    internal static AdmissionOutcome? TryReadAdmissionAnswer(this WireEnvelope envelope) =>
        envelope.Type switch
        {
            WireMessageType.JoinAccepted when envelope.HostPublicKey is not null => AdmissionOutcome.Accepted(envelope.HostPublicKey),
            WireMessageType.JoinDenied => AdmissionOutcome.Denied(),
            WireMessageType.JoinLapsed => AdmissionOutcome.Lapsed(),
            _ => null,
        };

    private static bool IsAddressedTo(WireEnvelope envelope, byte[]? ownPublicKey) =>
        envelope.PublicKey is { } addressee
        && ownPublicKey is not null
        && CryptographicOperations.FixedTimeEquals(addressee, ownPublicKey);

    public static AdmissionDeadline? TryGetDeadline(this WireEnvelope envelope) =>
        envelope.DeadlineUtcTicks is { } ticks ? AdmissionDeadline.TryFromWire(ticks) : null;

    public static SealedPayload? TryGetSealedPayload(this WireEnvelope envelope) =>
        envelope.Type == WireMessageType.SessionPayload && envelope.Nonce is not null && envelope.Payload is not null
            ? SealedPayload.FromWire(envelope.Nonce, envelope.Payload)
            : null;}
