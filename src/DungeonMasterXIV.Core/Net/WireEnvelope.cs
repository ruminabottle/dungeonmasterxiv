using System;
using System.Security.Cryptography;
using System.Text;

namespace DungeonMasterXIV.Net;

/// <summary>A message sent through the relay, with factories for each message type and its associated data.</summary>
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

    public byte[]? ReclaimHash { get; private init; }

    public byte[]? ReclaimSecret { get; private init; }

    public static byte[] AssociatedDataFor(SessionCode code, WireMessageType type) =>
        Encoding.UTF8.GetBytes($"{code.Value}:{(int)type}");

    public byte[] AssociatedData() =>
        Encoding.UTF8.GetBytes($"{SessionCode}:{(int)Type}");

    public static WireEnvelope ForCodeRequest(SessionCode code, byte[]? reclaimHash = null) =>
        new(WireMessageType.CodeRequest, code.Value) { ReclaimHash = reclaimHash };

    public static WireEnvelope ForReclaim(SessionCode code, byte[] reclaimSecret)
    {
        ArgumentNullException.ThrowIfNull(reclaimSecret);
        return new WireEnvelope(WireMessageType.Reclaim, code.Value) { ReclaimSecret = reclaimSecret };
    }

    public static WireEnvelope ForReclaimed(SessionCode code) => new(WireMessageType.Reclaimed, code.Value);

    public static WireEnvelope ForHostAway(SessionCode code) => new(WireMessageType.HostAway, code.Value);

    public static WireEnvelope ForHostBack(SessionCode code) => new(WireMessageType.HostBack, code.Value);

    public static WireEnvelope ForResume(SessionCode code, byte[] publicKey, SealedPayload proof)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(proof);
        return new WireEnvelope(WireMessageType.Resume, code.Value)
        {
            PublicKey = publicKey,
            Nonce = proof.Nonce,
            Payload = proof.Ciphertext,
        };
    }

    public static WireEnvelope ForCodeAccepted(SessionCode code) =>
        new(WireMessageType.CodeAccepted, code.Value);

    public static WireEnvelope ForCodeRefused(SessionCode code) =>
        new(WireMessageType.CodeRefused, code.Value);

    public static WireEnvelope ForJoinHello(SessionCode code, byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return new WireEnvelope(WireMessageType.JoinHello, code.Value) { PublicKey = publicKey };
    }

    public static WireEnvelope ForHostKey(SessionCode code, byte[] joinerPublicKey, byte[] hostPublicKey)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        return new WireEnvelope(WireMessageType.HostKey, code.Value)
        {
            PublicKey = joinerPublicKey,
            HostPublicKey = hostPublicKey,
        };
    }

    public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey, SealedPayload details)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(details);
        return new WireEnvelope(WireMessageType.JoinRequest, code.Value)
        {
            PublicKey = publicKey,
            Nonce = details.Nonce,
            Payload = details.Ciphertext,
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
            ReclaimHash = wire.ReclaimHash,
            ReclaimSecret = wire.ReclaimSecret,
        };

    public static WireEnvelope ForJoinAccepted(
        SessionCode code,
        byte[] joinerPublicKey,
        byte[] hostPublicKey,
        SealedPayload? welcome = null)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        return new WireEnvelope(WireMessageType.JoinAccepted, code.Value)
        {
            PublicKey = joinerPublicKey,
            HostPublicKey = hostPublicKey,
            Nonce = welcome?.Nonce,
            Payload = welcome?.Ciphertext,
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

    public static WireEnvelope ForConnectionDropped(SessionCode code, byte[] memberPublicKey)
    {
        ArgumentNullException.ThrowIfNull(memberPublicKey);
        return new WireEnvelope(WireMessageType.ConnectionDropped, code.Value)
        {
            PublicKey = memberPublicKey,
        };
    }
}
