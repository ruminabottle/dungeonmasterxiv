namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>One session a removed connection left: if it ended, its orphans; if not, its host and member key.</summary>
public readonly record struct SessionDeparture(
    string Code,
    bool EndedSession,
    IReadOnlyList<string> OrphanedConnections,
    string HostConnectionId = "",
    string? DepartedMemberKey = null);

/// <summary>The sessions a removed connection left, one departure for each.</summary>
public readonly record struct ConnectionRemoval(IReadOnlyList<SessionDeparture> Departures)
{
    public static ConnectionRemoval NotInSession => new([]);

    public IEnumerable<SessionDeparture> Ended => Departures.Where(departure => departure.EndedSession);
}
