using System;
using DungeonMasterXIV.Rolls;

namespace DungeonMasterXIV.Net;

/// <summary>A stream entry in the plain form sent inside session content, convertible back to an entry.</summary>
public readonly record struct StreamLine(
    long Sequence,
    long AtUtcTicks,
    StreamEventKind Kind,
    string Peer,
    string Text,
    SharedRoll? Roll = null)
{
    public static StreamLine From(StreamEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new StreamLine(
            entry.Stamp.Sequence, entry.Stamp.AtUtcTicks, entry.Kind, entry.Peer.Value, entry.Text, entry.Roll);
    }

    public bool TryToEntry(out StreamEntry entry)
    {
        entry = default!;

        if (Sequence < 1
            || !PeerCode.TryParse(Peer, out var peer)
            || (Roll is not null && !Roll.IsWithinBounds(RollLimits.Default)))
        {
            return false;
        }

        entry = new StreamEntry(new StreamStamp(Sequence, AtUtcTicks), Kind, peer, Text ?? string.Empty, Roll);
        return true;
    }
}
