using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Opens the participant id sealed into a join acceptance addressed to this client.</summary>
public static class ParticipantReceipt
{
    public static Guid? TryOpen(WireEnvelope? envelope, SessionKeyExchange? keys, SessionCode? code)
    {
        if (envelope is not { Type: WireMessageType.JoinAccepted, PublicKey: { } addressee, HostPublicKey: { } hostPublicKey }
            || keys is null
            || code is not { } sessionCode
            || !CryptographicOperations.FixedTimeEquals(addressee, keys.PublicKey)
            || !SessionKeyExchange.CanAgreeWith(hostPublicKey))
        {
            return null;
        }

        var key = keys.DeriveSharedKey(hostPublicKey, sessionCode);
        var details = JoinDetailsCodec.TryOpen(key, envelope);
        CryptographicOperations.ZeroMemory(key);

        return Guid.TryParseExact(details?.ParticipantId, "D", out var id) ? id : null;
    }
}
