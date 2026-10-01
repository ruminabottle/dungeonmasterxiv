using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>A peer the host has admitted, with its peer code, role, verification, public key and display name.</summary>
public sealed class AdmittedPeer
{
    private readonly byte[]? _publicKey;

    internal AdmittedPeer(
        PeerCode peerCode,
        SessionRole role,
        AdmissionVerification verification,
        byte[]? publicKey = null,
        DisplayName displayName = default)
    {
        PeerCode = peerCode;
        Role = role;
        Verification = verification;
        _publicKey = publicKey?.ToArray();
        DisplayName = displayName;
    }

    public byte[]? PublicKey => _publicKey?.ToArray();

    public DisplayName DisplayName { get; }

    public PeerCode PeerCode { get; }

    public SessionRole Role { get; }

    public AdmissionVerification Verification { get; }
}
