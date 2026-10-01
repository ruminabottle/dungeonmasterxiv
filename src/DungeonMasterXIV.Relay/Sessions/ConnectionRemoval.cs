namespace DungeonMasterXIV.Relay.Sessions;

public readonly record struct SessionDeparture(
    string Code,
    bool EndedSession,
    IReadOnlyList<string> OrphanedConnections,
    string HostConnectionId = "",
    string? DepartedMemberKey = null);

public readonly record struct ConnectionRemoval(IReadOnlyList<SessionDeparture> Departures)
{
    public static ConnectionRemoval NotInSession => new([]);

    public IEnumerable<SessionDeparture> Ended => Departures.Where(departure => departure.EndedSession);
}
