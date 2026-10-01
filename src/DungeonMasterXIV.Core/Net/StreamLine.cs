namespace DungeonMasterXIV.Net;

/// <summary>A stream entry in the plain form sent inside session content, convertible back to an entry.</summary>
public readonly record struct StreamLine(
    long Sequence,
    long AtUtcTicks,
    StreamEventKind Kind,
    string Peer,
    string Text)
{
    public bool TryToEntry(out StreamEntry entry)
    {
        entry = default!;

        if (Sequence < 1 || !PeerCode.TryParse(Peer, out var peer))
        {
            return false;
        }

        entry = new StreamEntry(new StreamStamp(Sequence, AtUtcTicks), Kind, peer, Text ?? string.Empty);
        return true;
    }
}
