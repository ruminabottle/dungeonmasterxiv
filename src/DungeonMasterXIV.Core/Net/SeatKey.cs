namespace DungeonMasterXIV.Net;

/// <summary>The identity entitlement is kept under: a member's participant id, or its peer code when it has none.</summary>
public static class SeatKey
{
    public static string For(AdmittedPeer peer) => peer.ParticipantId?.ToString("D") ?? peer.PeerCode.Value;
}
