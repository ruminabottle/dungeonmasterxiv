namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>One live session on the relay: its host connection, admitted members and pending joiners.</summary>
internal sealed class LiveSession(string hostConnectionId)
{
    public string HostConnectionId { get; } = hostConnectionId;

    public Dictionary<string, string> Members { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Pending { get; } = new(StringComparer.Ordinal);

    public bool IsHost(string connectionId) =>
        string.Equals(HostConnectionId, connectionId, StringComparison.Ordinal);

    public bool IsMember(string connectionId) => IsHost(connectionId) || Members.ContainsKey(connectionId);

    public bool HasPending(string connectionId) =>
        Pending.Values.Contains(connectionId, StringComparer.Ordinal);

    public IEnumerable<string> Everyone() => Members.Keys.Prepend(HostConnectionId);

    public void ForgetAllPending(string connectionId)
    {
        var stale = Pending
            .Where(entry => string.Equals(entry.Value, connectionId, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var key in stale)
        {
            Pending.Remove(key);
        }
    }
}
