using System;
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
        byte[]? publicKey = null,
        DisplayName displayName = default,
        Guid? participantId = null)
    {
        var existing = _admitted.FirstOrDefault(peer => peer.PeerCode == peerCode);
        if (existing is not null)
        {
            return existing;
        }

        var peer = new AdmittedPeer(peerCode, role, publicKey, displayName, participantId);
        _admitted.Add(peer);
        return peer;
    }

    public bool Remove(PeerCode peerCode)
    {
        var peer = _admitted.FirstOrDefault(candidate => candidate.PeerCode == peerCode);
        return peer is not null && _admitted.Remove(peer);
    }

    public bool IsAdmitted(PeerCode peerCode) => _admitted.Any(peer => peer.PeerCode == peerCode);

    public AdmittedPeer? HolderOf(Guid participantId) =>
        _admitted.FirstOrDefault(peer => peer.ParticipantId == participantId);

    public AdmittedPeer? Find(PeerCode peerCode) =>
        _admitted.FirstOrDefault(peer => peer.PeerCode == peerCode);

    public void Clear() => _admitted.Clear();
}
