using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>Announces a member's drop only once it outlasts the silent window, and their return only after that.</summary>
public sealed class DropAnnouncements
{
    private readonly MemberDrops _drops;
    private readonly HashSet<PeerCode> _announced = new();

    public DropAnnouncements(MemberDrops drops)
    {
        ArgumentNullException.ThrowIfNull(drops);

        _drops = drops;
    }

    /// <summary>The members whose drop has just outlasted the silent window, each at most once per drop.</summary>
    public IReadOnlyList<PeerCode> DueAt(DateTimeOffset now)
    {
        var due = new List<PeerCode>();
        foreach (var (peer, since) in _drops.Entries)
        {
            if (now - since >= ReconnectNotice.Silent && _announced.Add(peer))
            {
                due.Add(peer);
            }
        }

        return due;
    }

    /// <summary>True when this member's drop was announced, so their return is too; forgets the drop either way.</summary>
    public bool Returned(PeerCode peer) => _announced.Remove(peer);

    public void Forget(PeerCode peer) => _announced.Remove(peer);

    public void Clear() => _announced.Clear();
}
