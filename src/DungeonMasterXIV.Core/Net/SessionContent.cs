using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

public sealed class SessionContent
{
    public IReadOnlyList<RosterEntry>? Roster { get; init; }

    public long? ClosingAtUtcTicks { get; init; }

    public bool? Leaving { get; init; }

    public string? Saying { get; init; }

    public IReadOnlyList<StreamLine>? Entries { get; init; }
}

public readonly record struct RosterEntry(string PeerCode, string DisplayName, SessionRole Role);
