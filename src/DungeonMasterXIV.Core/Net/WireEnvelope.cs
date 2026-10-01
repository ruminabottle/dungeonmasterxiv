using System;
using System.Security.Cryptography;
using System.Text;

namespace DungeonMasterXIV.Net;

public sealed record WireEnvelope
{
    private WireEnvelope(WireMessageType type, string sessionCode)
    {
        Type = type;
        SessionCode = sessionCode;
    }

    public WireMessageType Type { get; private init; }

    public string SessionCode { get; private init; }

    public byte[]? Nonce { get; private init; }

    public byte[]? Payload { get; private init; }

    public byte[]? PublicKey { get; private init; }

    public byte[]? HostPublicKey { get; private init; }

    public long? DeadlineUtcTicks { get; private init; }

    public string? ClaimedParticipantId { get; private init; }

    public string? ParticipantId { get; private init; }

    public static byte[] AssociatedDataFor(SessionCode code, WireMessageType type) =>
        Encoding.UTF8.GetBytes($"{code.Value}:{(int)type}");

    public byte[] AssociatedData() =>
        Encoding.UTF8.GetBytes($"{SessionCode}:{(int)Type}");

    public static WireEnvelope ForCodeRequest(SessionCode code) =>
        new(WireMessageType.CodeRequest, code.Value);

    public static WireEnvelope ForCodeAccepted(SessionCode code) =>
        new(WireMessageType.CodeAccepted, code.Value);

    public static WireEnvelope ForCodeRefused(SessionCode code) =>
        new(WireMessageType.CodeRefused, code.Value);

    public string? DisplayName { get; private init; }

    public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey) =>
        ForJoinRequest(code, publicKey, DungeonMasterXIV.Net.DisplayName.None);

    public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey, DisplayName name)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return new WireEnvelope(WireMessageType.JoinRequest, code.Value)
        {
            PublicKey = publicKey,
            DisplayName = name.WasStated ? name.Value : null,
        };
    }

    public static WireEnvelope ForRelinkRequest(SessionCode code, byte[] publicKey, Guid claimedParticipantId)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return new WireEnvelope(WireMessageType.JoinRequest, code.Value)
        {
            PublicKey = publicKey,
            ClaimedParticipantId = claimedParticipantId.ToString("D"),
        };
    }

    public static WireEnvelope ForSessionPayload(SessionCode code, SealedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new WireEnvelope(WireMessageType.SessionPayload, code.Value)
        {
            Nonce = payload.Nonce,
            Payload = payload.Ciphertext,
        };
    }

    internal static WireEnvelope FromWire(WireMessageType type, string sessionCode, WireShape wire) =>
        new(type, sessionCode)
        {
            Nonce = wire.Nonce,
            Payload = wire.Payload,
            PublicKey = wire.PublicKey,
            HostPublicKey = wire.HostPublicKey,
            DeadlineUtcTicks = wire.DeadlineUtcTicks,
            DisplayName = wire.DisplayName,
            ClaimedParticipantId = wire.ClaimedParticipantId,
            ParticipantId = wire.ParticipantId,
        };

    public static WireEnvelope ForJoinAccepted(
        SessionCode code,
        byte[] joinerPublicKey,
        byte[] hostPublicKey,
        Guid? participantId = null)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        return new WireEnvelope(WireMessageType.JoinAccepted, code.Value)
        {
            PublicKey = joinerPublicKey,
            HostPublicKey = hostPublicKey,
            ParticipantId = participantId?.ToString("D"),
        };
    }

    public static WireEnvelope ForJoinDenied(SessionCode code, byte[] joinerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        return new WireEnvelope(WireMessageType.JoinDenied, code.Value) { PublicKey = joinerPublicKey };
    }

    public static WireEnvelope ForJoinLapsed(SessionCode code, byte[] joinerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        return new WireEnvelope(WireMessageType.JoinLapsed, code.Value) { PublicKey = joinerPublicKey };
    }

    public static WireEnvelope ForJoinPending(
        SessionCode code,
        byte[] joinerPublicKey,
        byte[] hostPublicKey,
        AdmissionDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        return new WireEnvelope(WireMessageType.JoinPending, code.Value)
        {
            PublicKey = joinerPublicKey,
            HostPublicKey = hostPublicKey,
            DeadlineUtcTicks = deadline.UtcTicks,
        };
    }

    public static WireEnvelope ForJoinerHoldsFingerprint(SessionCode code, byte[] joinerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        return new WireEnvelope(WireMessageType.JoinerHoldsFingerprint, code.Value)
        {
            PublicKey = joinerPublicKey,
        };
    }

    public static WireEnvelope ForConnectionDropped(SessionCode code, byte[] memberPublicKey)
    {
        ArgumentNullException.ThrowIfNull(memberPublicKey);
        return new WireEnvelope(WireMessageType.ConnectionDropped, code.Value)
        {
            PublicKey = memberPublicKey,
        };
    }
}
