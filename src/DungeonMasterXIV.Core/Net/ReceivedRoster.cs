using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>Keeps the latest roster a member has received from the host.</summary>
internal sealed class ReceivedRoster
{
    public IReadOnlyList<RosterEntry> Entries { get; private set; } = [];

    public void Replace(IReadOnlyList<RosterEntry>? entries) => Entries = entries ?? Entries;
}
