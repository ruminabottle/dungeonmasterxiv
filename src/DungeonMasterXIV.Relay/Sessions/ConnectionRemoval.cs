namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>
/// What a connection leaving did to one session it was part of.
/// </summary>
/// <param name="Code">The session it left.</param>
/// <param name="EndedSession">
/// True if it hosted this one, in which case the session is over and the code is free again.
/// </param>
/// <param name="OrphanedConnections">
/// Members and pending joiners left behind when a host leaves. They are detached from <b>this</b>
/// session only — a connection orphaned here may still legitimately be in another — and the relay
/// tells them nothing: the plugin distinguishes host-lost from relay-down from code-not-active on
/// its own side (R-1.8), and a relay narrating session lifecycle would be asserting authority D-3
/// denies it.
/// </param>
public readonly record struct SessionDeparture(
    string Code,
    bool EndedSession,
    IReadOnlyList<string> OrphanedConnections);

/// <summary>
/// Everything a connection leaving did, across every session it was part of.
/// </summary>
/// <remarks>
/// A list rather than one session, because a connection has a <b>set</b> of roles rather than one.
/// A DM who hosts a session and then joins someone else's is an ordinary user, not an exotic case,
/// and their single transport is a host in one session and a joiner in another at the same time.
/// A removal that unwound only one of those would leave the other stranded, so this type exists to
/// make "which one did it leave" an impossible question to ask.
/// </remarks>
/// <param name="Departures">One entry per session the connection was part of.</param>
public readonly record struct ConnectionRemoval(IReadOnlyList<SessionDeparture> Departures)
{
    /// <summary>The connection was not part of any session.</summary>
    public static ConnectionRemoval NotInSession => new([]);

    /// <summary>Sessions this connection's departure ended, because it hosted them.</summary>
    public IEnumerable<SessionDeparture> Ended => Departures.Where(departure => departure.EndedSession);
}
