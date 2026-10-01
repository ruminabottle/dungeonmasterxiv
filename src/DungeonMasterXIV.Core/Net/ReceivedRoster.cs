using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

internal sealed class ReceivedRoster
{
    public IReadOnlyList<RosterEntry> Entries { get; private set; } = [];

    public void Replace(IReadOnlyList<RosterEntry>? entries) => Entries = entries ?? Entries;
}
