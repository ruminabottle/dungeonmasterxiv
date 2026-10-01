using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Reads the participant ID from a join acceptance addressed to this client's public key.</summary>
public static class ParticipantReceipt
{
    public static Guid? TryRead(WireEnvelope? envelope, byte[]? ownPublicKey) =>
        envelope is { Type: WireMessageType.JoinAccepted, PublicKey: { } addressee }
        && ownPublicKey is not null
        && CryptographicOperations.FixedTimeEquals(addressee, ownPublicKey)
        && Guid.TryParseExact(envelope.ParticipantId, "D", out var id)
            ? id
            : null;
}
