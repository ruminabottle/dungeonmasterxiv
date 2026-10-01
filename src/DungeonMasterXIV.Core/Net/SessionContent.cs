using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>The decrypted body of a session payload: roster, closing time, leaving flag, message, or entries.</summary>
public sealed class SessionContent
{
    public IReadOnlyList<RosterEntry>? Roster { get; init; }

    public long? ClosingAtUtcTicks { get; init; }

    public bool? Leaving { get; init; }

    public string? Saying { get; init; }

    public IReadOnlyList<StreamLine>? Entries { get; init; }
}

/// <summary>One player in a session roster: peer code, display name and role.</summary>
public readonly record struct RosterEntry(string PeerCode, string DisplayName, SessionRole Role);
