namespace DungeonMasterXIV.Net;

internal sealed class WireShape
{
    public WireMessageType Type { get; set; }

    public string? SessionCode { get; set; }

    public byte[]? Nonce { get; set; }

    public byte[]? Payload { get; set; }

    public byte[]? PublicKey { get; set; }

    public byte[]? HostPublicKey { get; set; }

    public long? DeadlineUtcTicks { get; set; }

    public string? DisplayName { get; set; }

    public string? ClaimedParticipantId { get; set; }

    public string? ParticipantId { get; set; }
}
