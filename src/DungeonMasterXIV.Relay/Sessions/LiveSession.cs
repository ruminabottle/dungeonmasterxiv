namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>
/// One live session's people: who hosts it, who has been admitted, and who is still waiting on the
/// host's decision.
/// </summary>
/// <remarks>
/// Split from <see cref="SessionRegistry"/> because they answer different questions. This type knows
/// who is in <i>one</i> session; the registry knows which codes exist and which sessions a given
/// connection touches. Keeping the second question out of here is what lets a connection hold
/// different roles in different sessions without either type needing to know that.
/// </remarks>
/// <param name="hostConnectionId">The connection that claimed this session's code.</param>
internal sealed class LiveSession(string hostConnectionId)
{
    /// <summary>The host. Ends the session when it leaves.</summary>
    public string HostConnectionId { get; } = hostConnectionId;

    /// <summary>Admitted connections, excluding the host. These are routed; nothing else is.</summary>
    /// <summary>
    /// Every admitted member, connection id to the base64 public key it joined with.
    /// </summary>
    /// <remarks>
    /// <b>A map rather than a set, because the relay had no way to NAME a member to its host.</b>
    /// Admission used to drop the key: <c>Pending</c> is keyed by it, <c>TryResolvePending</c>
    /// removed the entry and added the connection id to a set, and the key was gone. Connection ids
    /// are relay-internal and mean nothing to a host, so a drop notice built from one would have
    /// been unusable — the mechanism was missing rather than broken (A-1.28).
    /// <para>
    /// <b>The relay keeps the key and nothing else about a member.</b> Not a name, not a participant
    /// id, not a peer code — the host derives that itself. Retaining the one value the two of them
    /// already share is what lets the relay say WHAT IT SAW without asserting WHO IT WAS.
    /// </para>
    /// </remarks>
    public Dictionary<string, string> Members { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Connections waiting on the host, by the SPKI key they presented. Held so a decision can find
    /// them, and routed to by nothing.
    /// </summary>
    /// <remarks>
    /// Keyed by public key rather than by connection because one connection may have more than one
    /// request outstanding, and because the key is what an admission names — the relay must not mint
    /// an identifier of its own for a participant (D-3).
    /// </remarks>
    public Dictionary<string, string> Pending { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether this connection hosts the session.</summary>
    public bool IsHost(string connectionId) =>
        string.Equals(HostConnectionId, connectionId, StringComparison.Ordinal);

    /// <summary>Whether this connection may send and receive session traffic here.</summary>
    public bool IsMember(string connectionId) => IsHost(connectionId) || Members.ContainsKey(connectionId);

    /// <summary>Whether this connection has any request still outstanding here.</summary>
    public bool HasPending(string connectionId) =>
        Pending.Values.Contains(connectionId, StringComparer.Ordinal);

    /// <summary>Everyone entitled to session traffic: the host and every admitted member.</summary>
    public IEnumerable<string> Everyone() => Members.Keys.Prepend(HostConnectionId);

    /// <summary>
    /// Removes <b>every</b> pending entry naming this connection, not the first one found.
    /// </summary>
    /// <remarks>
    /// A connection can be pending under more than one key — it may retry a join with a fresh
    /// ephemeral key, which is the ordinary thing to do after a lapse (R-1.3c). Stopping at the
    /// first match leaves the connection admittable after it has left, and admitting it then puts a
    /// dead id into <see cref="Members"/> where nothing removes it.
    /// </remarks>
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
