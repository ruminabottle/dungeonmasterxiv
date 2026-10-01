using DungeonMasterXIV.Relay.Sessions;
using Microsoft.Extensions.Logging;

namespace DungeonMasterXIV.Relay.Diagnostics;

/// <summary>Writes the relay's connection and routing events to the log.</summary>
public sealed class RelayLog(ILogger<RelayLog> logger)
{
    private readonly ILogger<RelayLog> _logger = logger;

    public void ConnectionOpened(string connectionId) =>
        _logger.LogInformation("connection {ConnectionId} opened", connectionId);

    public void ConnectionClosed(string connectionId, ConnectionRemoval removal, string reason)
    {
        if (removal.Departures.Count == 0)
        {
            _logger.LogInformation("connection {ConnectionId} closed: {Reason}; in no session", connectionId, reason);
            return;
        }

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

    public void ConnectionRejected(string connectionId, string reason) =>
        _logger.LogWarning("connection {ConnectionId} rejected: {Reason}", connectionId, reason);

    public void Routed(string connectionId, string sessionCode, RelayDecision decision)
    {
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

    public void ConnectionFaulted(string connectionId, Exception exception) =>
        _logger.LogError(exception, "connection {ConnectionId} faulted", connectionId);
}
