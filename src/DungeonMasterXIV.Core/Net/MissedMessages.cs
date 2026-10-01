using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Holds stream entries a member missed while away, and replays them with a gap marker when needed.</summary>
internal sealed class MissedMessages
{
    private readonly Dictionary<PeerCode, List<StreamEntry>> _held = new();
    private readonly HashSet<PeerCode> _gapped = new();

    public bool IsHoldingFor(PeerCode member) => _held.ContainsKey(member) || _gapped.Contains(member);

    public void Hold(PeerCode member, StreamEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        RefuseAnAbsentCode(member);

        if (!_held.TryGetValue(member, out var entries))
        {
            _held[member] = entries = new List<StreamEntry>();
        }

        entries.Add(entry);
    }

    public void NoteGap(PeerCode member)
    {
        RefuseAnAbsentCode(member);
        _gapped.Add(member);
    }

    private static void RefuseAnAbsentCode(PeerCode member)
    {
        if (!member.IsPresent)
        {
            throw new ArgumentException(
                "the peer code is absent, so this is not a member this can hold for or mark a gap "
                + "against. Every absent code is the same key, so accepting one merges members' "
                + "streams rather than losing them. See PeerCode's remarks on default.",
                nameof(member));
        }
    }

    public IReadOnlyList<StreamEntry> Replay(PeerCode member, Func<StreamStamp> stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);

        var held = _held.TryGetValue(member, out var entries) ? entries : new List<StreamEntry>();
        var marker = _gapped.Contains(member)
            ? new[] { new StreamEntry(stamp(), StreamEventKind.Gap, member, string.Empty) }
            : [];

        _held.Remove(member);
        _gapped.Remove(member);

        return marker.Concat(held).ToList();
    }

    public void Forget(PeerCode member)
    {
        _held.Remove(member);
        _gapped.Remove(member);
    }
}
