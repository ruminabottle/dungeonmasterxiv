namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>One session on the relay: its host connection (absent while held), members, pending joiners and reclaim hash.</summary>
internal sealed class LiveSession(string hostConnectionId, byte[]? reclaimHash)
{
    public string? HostConnectionId { get; set; } = hostConnectionId;

    public byte[]? ReclaimHash { get; } = reclaimHash;

    public DateTimeOffset? HostAwaySince { get; set; }

    public bool HostAway => HostAwaySince is not null;

    public Dictionary<string, string> Members { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Pending { get; } = new(StringComparer.Ordinal);

    public List<string> DroppedWhileAway { get; } = new();

    public bool IsHost(string connectionId) =>
        HostConnectionId is not null && string.Equals(HostConnectionId, connectionId, StringComparison.Ordinal);

    public bool IsMember(string connectionId) => IsHost(connectionId) || Members.ContainsKey(connectionId);

    public bool HasPending(string connectionId) =>
        Pending.Values.Contains(connectionId, StringComparer.Ordinal);

    public IEnumerable<string> Everyone() =>
        HostConnectionId is { } host ? Members.Keys.Prepend(host) : Members.Keys;

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
