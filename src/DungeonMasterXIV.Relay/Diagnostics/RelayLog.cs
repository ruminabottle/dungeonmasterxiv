using DungeonMasterXIV.Relay.Sessions;
using Microsoft.Extensions.Logging;

namespace DungeonMasterXIV.Relay.Diagnostics;

/// <summary>
/// The relay's forensic log: enough for QA to read the outcome of a connection attempt, and the
/// reason for a failure, without a human present (A-1.5a-r).
/// </summary>
/// <remarks>
/// <para>
/// <b>Where this goes, and why that is not a dodge of A-1.5e.</b> These lines go to stdout and
/// nowhere else. The relay opens no log file, and the container definition configures no file sink,
/// so nothing the relay runs writes to disk — which is what A-1.5e asserts. A container runtime
/// that captures stdout is a separate party retaining its own capture, on the operator's side of
/// the line, and E-8 requires the service policy to state retention. The distinction is real but it
/// is thin, so it is written here rather than left for someone to discover: <b>if this type ever
/// gains a file sink, A-1.5e is false and so is R-1.7a's shipped copy.</b>
/// </para>
/// <para>
/// <b>What may not appear in a line, per D-8.</b> No character name — structurally impossible here,
/// since names live inside payloads the relay cannot decrypt, and worth stating because the
/// guarantee should not rest on nobody having thought to add one. No network address, not even
/// hashed: the address is the correlator D-11's rationale names by name ("same address, two
/// codes"), and a per-session salted pseudonym was considered and dropped because nothing
/// A-1.5a-r needs is worth carrying that risk to get.
/// </para>
/// <para>
/// A connection is named by an id generated fresh when it opens and thrown away when it closes.
/// It correlates lines within one connection, which is what diagnosing a failed join takes, and
/// correlates nothing across two session codes. Session codes DO appear: the standards direct that
/// a log line names the session-scoped code in place of a person, and R-1.2a scopes a code to a
/// live session rather than to a player.
/// </para>
/// <para>
/// Message routing is logged at Debug and is off by default. Every forwarded payload at
/// Information would make this a traffic log — a record of who spoke to whom and when, accumulating
/// for as long as the process runs — which is a different artifact from the failure forensics
/// A-1.5a-r asks for, and a worse one to own.
/// </para>
/// </remarks>
public sealed class RelayLog(ILogger<RelayLog> logger)
{
    private readonly ILogger<RelayLog> _logger = logger;

    /// <summary>A client connected. Says nothing about who or from where.</summary>
    public void ConnectionOpened(string connectionId) =>
        _logger.LogInformation("connection {ConnectionId} opened", connectionId);

    /// <summary>
    /// A client disconnected, and what that did to its session. The reason is a transport-level
    /// description — a close status, a timeout — never anything from a payload.
    /// </summary>
    public void ConnectionClosed(string connectionId, ConnectionRemoval removal, string reason)
    {
        if (removal.Departures.Count == 0)
        {
            _logger.LogInformation("connection {ConnectionId} closed: {Reason}; in no session", connectionId, reason);
            return;
        }

        // One line per session, because a connection can be the host of one and a joiner in another
        // and a single line would have to pick which to report. QA reading this after a failed
        // attempt needs every session the connection was in, not the first one it happened to hold.
        foreach (var departure in removal.Departures)
        {
            _logger.LogInformation(
                "connection {ConnectionId} closed: {Reason}; session {SessionCode}, ended={EndedSession}, orphaned={OrphanedCount}",
                connectionId,
                reason,
                departure.Code,
                departure.EndedSession,
                departure.OrphanedConnections.Count);
        }
    }

    /// <summary>The relay refused a connection before any session existed — the A-1.5b failure path.</summary>
    public void ConnectionRejected(string connectionId, string reason) =>
        _logger.LogWarning("connection {ConnectionId} rejected: {Reason}", connectionId, reason);

    /// <summary>
    /// A routing decision. The outcomes that answer "did the join work, and if not why" are
    /// Information; ordinary payload traffic is Debug so the default log does not become a record
    /// of who spoke to whom.
    /// </summary>
    public void Routed(string connectionId, string sessionCode, RelayDecision decision)
    {
        // Payload traffic and unrecognised types are both ordinary. The first would make this a
        // record of who spoke to whom; the second is D-14 working as intended.
        var level = decision.Outcome is RelayOutcome.PayloadForwarded or RelayOutcome.UnrecognisedMessageType
            ? LogLevel.Debug
            : LogLevel.Information;

        _logger.Log(
            level,
            "connection {ConnectionId} session {SessionCode}: {Outcome} ({Action}, {RecipientCount} recipients)",
            connectionId,
            sessionCode,
            decision.Outcome,
            decision.Action,
            decision.Recipients.Count);
    }

    /// <summary>
    /// Something threw while handling a connection. Logged with context rather than swallowed, per
    /// the standards; the exception message is transport-level and carries no payload.
    /// </summary>
    public void ConnectionFaulted(string connectionId, Exception exception) =>
        _logger.LogError(exception, "connection {ConnectionId} faulted", connectionId);
}
