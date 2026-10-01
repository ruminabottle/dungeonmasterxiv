using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

public sealed class MemberDrops
{
    private readonly Dictionary<PeerCode, DateTimeOffset> _dropped = new();

    public int Count => _dropped.Count;

    public void Record(PeerCode peerCode, DateTimeOffset when) => _dropped[peerCode] = when;

    public bool Forget(PeerCode peerCode) => _dropped.Remove(peerCode);

    public DateTimeOffset? WhenDropped(PeerCode peerCode) =>
        _dropped.TryGetValue(peerCode, out var when) ? when : null;

    public void Clear() => _dropped.Clear();
}
