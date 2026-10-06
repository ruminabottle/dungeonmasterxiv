using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>A private entry's audience, the seats entitled to it when it was sent, its placeholder text, and who revealed it.</summary>
public sealed record EntryPrivacy(
    MessageAudience Audience,
    IReadOnlyCollection<string> Entitled,
    string Placeholder,
    string? RevealedBy = null)
{
    public bool IsRevealed => RevealedBy is not null;

    public bool Entitles(string seat) => Entitled.Contains(seat);
}
