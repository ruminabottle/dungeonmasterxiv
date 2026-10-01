namespace DungeonMasterXIV.Data;

public readonly record struct LoggedEntry(LoggedStamp Stamp, string Kind, string Peer, string Text);

public readonly record struct LoggedStamp(long Sequence, long AtUtcTicks);
