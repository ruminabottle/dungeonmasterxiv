using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>
/// The connections currently attached to the relay, by id.
/// </summary>
/// <remarks>
/// In memory, for the process lifetime, like everything else the relay knows (D-2, A-1.5e). A
/// connection is added when its socket opens and removed when it closes; nothing outlives that.
/// </remarks>
public sealed class ConnectionDirectory
{
    private readonly ConcurrentDictionary<string, IRelayConnection> _connections = new(StringComparer.Ordinal);

    /// <summary>How many clients are attached. Diagnostics and tests only.</summary>
    public int Count => _connections.Count;

    /// <summary>Registers a newly opened connection.</summary>
    public void Add(IRelayConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connections[connection.Id] = connection;
    }

    /// <summary>Forgets a closed connection.</summary>
    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    /// <summary>Finds a connection by id, if it is still attached.</summary>
    public bool TryGet(string connectionId, [NotNullWhen(true)] out IRelayConnection? connection) =>
        _connections.TryGetValue(connectionId, out connection);
}
