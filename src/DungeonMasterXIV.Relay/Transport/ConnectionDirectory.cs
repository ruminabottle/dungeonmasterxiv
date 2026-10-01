using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>Holds the relay's open connections, keyed by connection id.</summary>
public sealed class ConnectionDirectory
{
    private readonly ConcurrentDictionary<string, IRelayConnection> _connections = new(StringComparer.Ordinal);

    public int Count => _connections.Count;

    public void Add(IRelayConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connections[connection.Id] = connection;
    }

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public bool TryGet(string connectionId, [NotNullWhen(true)] out IRelayConnection? connection) =>
        _connections.TryGetValue(connectionId, out connection);
}
