using System;

namespace DungeonMasterXIV.Net;

/// <summary>Stamps a host stream entry and sends it to every admitted member in the same step.</summary>
internal sealed class HostStream(SessionRecording recording, RosterBroadcast roster)
{
    public StreamEntry? Announce(
        StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at, SharedRoll? roll = null)
    {
        if (recording.StampAsHost(kind, peer, text, at, roll) is not { } entry)
        {
            return null;
        }

        roster.PublishEntry(StreamLine.From(entry));
        return entry;
    }
}
