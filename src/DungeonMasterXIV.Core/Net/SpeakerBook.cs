using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>A stream line's speaker as drawn: the display name the session knows them by, and their role.</summary>
public readonly record struct SpeakerName(string Name, SessionRole Role);

/// <summary>Remembers each peer's name and role from the rosters seen, so a line still names someone who has left.</summary>
public sealed class SpeakerBook
{
    private readonly Dictionary<string, SpeakerName> _known = new(StringComparer.Ordinal);

    public void Learn(IEnumerable<RosterEntry> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        foreach (var entry in roster)
        {
            _known[entry.PeerCode] = new SpeakerName(DisplayName.OrNone(entry.DisplayName).Value, entry.Role);
        }
    }

    public SpeakerName For(string peer) =>
        _known.TryGetValue(peer, out var known) ? known : new SpeakerName(DisplayName.Unstated, SessionRole.Player);
}
