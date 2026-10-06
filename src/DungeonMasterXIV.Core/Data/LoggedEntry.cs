namespace DungeonMasterXIV.Data;

/// <summary>One line of a session log: its stamp, event kind, peer, text, and privacy when it was not public.</summary>
public readonly record struct LoggedEntry(
    LoggedStamp Stamp,
    string Kind,
    string Peer,
    string Text,
    string? Audience = null,
    string? To = null,
    string? RevealedBy = null,
    long? RevealedAtUtcTicks = null);

/// <summary>Where a logged entry falls: its sequence number and its UTC time in ticks.</summary>
public readonly record struct LoggedStamp(long Sequence, long AtUtcTicks);
