using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>The list of peers the host has admitted to the session.</summary>
public sealed class SessionAudience
{
    private readonly List<AdmittedPeer> _admitted = new();

    public IReadOnlyList<AdmittedPeer> Recipients => _admitted.AsReadOnly();

    public int Count => _admitted.Count;

    public AdmittedPeer Admit(
        PeerCode peerCode,
        SessionRole role = SessionRole.Player,
        AdmissionVerification verification = AdmissionVerification.NotCompared,
        byte[]? publicKey = null,
        DisplayName displayName = default)
    {
        var existing = _admitted.FirstOrDefault(peer => peer.PeerCode == peerCode);
        if (existing is not null)
        {
            return existing;
        }

        var peer = new AdmittedPeer(peerCode, role, verification, publicKey, displayName);
        _admitted.Add(peer);
        return peer;
    }

    public bool Remove(PeerCode peerCode)
    {
        var peer = _admitted.FirstOrDefault(candidate => candidate.PeerCode == peerCode);
        return peer is not null && _admitted.Remove(peer);
    }

    public bool IsAdmitted(PeerCode peerCode) => _admitted.Any(peer => peer.PeerCode == peerCode);

    public AdmittedPeer? Find(PeerCode peerCode) =>
        _admitted.FirstOrDefault(peer => peer.PeerCode == peerCode);

    public int ConfirmedCount =>
        _admitted.Count(peer => peer.Verification == AdmissionVerification.Confirmed);

    public void Clear() => _admitted.Clear();
}
