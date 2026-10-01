namespace DungeonMasterXIV.Net;

/// <summary>One event in a session stream: its stamp, kind, peer and text.</summary>
public sealed record StreamEntry(
    StreamStamp Stamp,
    StreamEventKind Kind,
    PeerCode Peer,
    string Text);
