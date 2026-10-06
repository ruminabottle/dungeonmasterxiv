namespace DungeonMasterXIV.Net;

/// <summary>One event in a session stream: its stamp, kind, peer, text, roll, and privacy when it is not public.</summary>
public sealed record StreamEntry(
    StreamStamp Stamp,
    StreamEventKind Kind,
    PeerCode Peer,
    string Text,
    SharedRoll? Roll = null,
    EntryPrivacy? Privacy = null);
