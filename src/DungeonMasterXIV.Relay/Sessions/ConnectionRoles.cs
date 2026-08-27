namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>
/// Which sessions each connection is part of, and which one it hosts.
/// </summary>
/// <remarks>
/// <para>
/// The index that runs the other way from <see cref="LiveSession"/>: that type knows who is in one
/// session, this one knows which sessions one connection is in. Both are needed and neither derives
/// from the other — a disconnect arrives knowing only a connection id, and unwinding it has to find
/// every session that id appears in without walking all of them.
/// </para>
/// <para>
/// <b>A set, not a slot.</b> One connection is legitimately the host of one session and a joiner in
/// another, because <c>SessionCoordinator</c> drives hosting and joining over a single transport —
/// a DM who starts a session and then joins someone else's. Holding one code per connection made
/// the second role overwrite the first, which stranded the first session's code for the lifetime of
/// the process.
/// </para>
/// <para>Not thread-safe on its own; <see cref="SessionRegistry"/> owns the lock.</para>
/// </remarks>
internal sealed class ConnectionRoles
{
    private readonly Dictionary<string, HashSet<string>> _codesByConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _hostedByConnection = new(StringComparer.Ordinal);

    /// <summary>Whether this connection already hosts a session. One at a time is the limit.</summary>
    public bool Hosts(string connectionId) => _hostedByConnection.ContainsKey(connectionId);

    /// <summary>Whether this connection is in this session in any role, including pending.</summary>
    public bool IsIn(string connectionId, string code) =>
        _codesByConnection.TryGetValue(connectionId, out var codes) && codes.Contains(code);

    /// <summary>Records that this connection hosts this session.</summary>
    public void AddHost(string connectionId, string code)
    {
        _hostedByConnection[connectionId] = code;
        Add(connectionId, code);
    }

    /// <summary>Records that this connection is in this session in some role.</summary>
    public void Add(string connectionId, string code)
    {
        if (!_codesByConnection.TryGetValue(connectionId, out var codes))
        {
            codes = new HashSet<string>(StringComparer.Ordinal);
            _codesByConnection[connectionId] = codes;
        }

        codes.Add(code);
    }

    /// <summary>
    /// Removes this connection from one session only. An orphan of an ended session may still be
    /// hosting or joined elsewhere, so clearing its whole set here is how ending one session would
    /// strand another.
    /// </summary>
    public void Remove(string connectionId, string code)
    {
        if (!_codesByConnection.TryGetValue(connectionId, out var codes))
        {
            return;
        }

        codes.Remove(code);
        if (codes.Count == 0)
        {
            Forget(connectionId);
        }
    }

    /// <summary>
    /// Takes this connection out entirely, returning every session it was in so each can be
    /// unwound. Returns <c>null</c> if it was in none.
    /// </summary>
    public IReadOnlyCollection<string>? Forget(string connectionId)
    {
        _hostedByConnection.Remove(connectionId);
        return _codesByConnection.Remove(connectionId, out var codes) ? codes : null;
    }
}
