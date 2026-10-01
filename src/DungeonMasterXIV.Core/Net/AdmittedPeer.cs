using System;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>A peer the host has admitted, with its peer code, role, public key, display name and participant id.</summary>
public sealed class AdmittedPeer
{
    private readonly byte[]? _publicKey;

    internal AdmittedPeer(
        PeerCode peerCode,
        SessionRole role,
        byte[]? publicKey = null,
        DisplayName displayName = default,
        Guid? participantId = null)
    {
        PeerCode = peerCode;
        Role = role;
        _publicKey = publicKey?.ToArray();
        DisplayName = displayName;
        ParticipantId = participantId;
    }

    public byte[]? PublicKey => _publicKey?.ToArray();

    public DisplayName DisplayName { get; }

    public PeerCode PeerCode { get; }

    public SessionRole Role { get; }

    public Guid? ParticipantId { get; }
}
