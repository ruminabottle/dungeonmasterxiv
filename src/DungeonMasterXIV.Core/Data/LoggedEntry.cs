namespace DungeonMasterXIV.Data;

/// <summary>One line of a session log: its stamp, event kind, peer and text.</summary>
public readonly record struct LoggedEntry(LoggedStamp Stamp, string Kind, string Peer, string Text);

/// <summary>Where a logged entry falls: its sequence number and its UTC time in ticks.</summary>
public readonly record struct LoggedStamp(long Sequence, long AtUtcTicks);
