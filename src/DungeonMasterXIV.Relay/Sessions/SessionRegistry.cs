using System.Diagnostics.CodeAnalysis;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>
/// Every live session the relay is carrying: which codes are in use, who hosts each one, who has
/// been admitted, and who is still waiting on the DM.
/// </summary>
/// <remarks>
/// <para>
/// This is the relay-wide namespace R-1.2a puts here rather than on the host, because a host cannot
/// know what is free. It is also the whole of the relay's memory.
/// </para>
/// <para>
/// <b>A connection has a SET of roles, not one.</b> <c>SessionCoordinator</c> drives hosting and
/// joining over a single transport, so one connection is legitimately the host of one session and a
/// joiner in another — a DM who starts a session and then joins someone else's. Every lookup and
/// every unwind path below is therefore keyed on (connection, code) rather than on connection
/// alone, and <see cref="Remove"/> is total over the set. A model with one slot per connection
/// silently overwrote the first role with the second, which stranded the first session's code for
/// the lifetime of the process.
/// </para>
/// <para>
/// <b>In memory only, and that is a requirement rather than an implementation choice (D-2, A-1.5e).</b>
/// Nothing here is written anywhere and everything dies with the process — a relay that restarts has
/// never heard of any code, which is what makes it non-authoritative under D-3 and R-1.8. Note that
/// the no-write test cannot see a defect in this type: leaking sessions here is memory, not a file,
/// and the instrument built for D-2 was never going to catch it.
/// </para>
/// </remarks>
public sealed class SessionRegistry
{
    private readonly Lock _gate = new();

    /// <summary>Live sessions, by code.</summary>
    private readonly Dictionary<string, LiveSession> _byCode = new(StringComparer.Ordinal);

    /// <summary>Which sessions each connection is in. The set that makes unwinding total.</summary>
    private readonly ConnectionRoles _roles = new();

    /// <summary>How many sessions are live. Diagnostics and tests; the relay never reports it out.</summary>
    public int LiveSessionCount
    {
        get
        {
            lock (_gate)
            {
                return _byCode.Count;
            }
        }
    }

    /// <summary>
    /// Claims <paramref name="code"/> for <paramref name="hostConnectionId"/>, or reports it taken.
    /// This is the arbitration R-1.2a assigns to the relay: the host proposes, the relay decides.
    /// </summary>
    /// <remarks>
    /// Refused if this connection already hosts a session — one client hosting two at once is not a
    /// case the plugin produces. Being a joiner elsewhere is no obstacle and must not be.
    /// </remarks>
    public bool TryClaim(SessionCode code, string hostConnectionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostConnectionId);

        lock (_gate)
        {
            if (_byCode.ContainsKey(code.Value) || _roles.Hosts(hostConnectionId))
            {
                return false;
            }

            _byCode[code.Value] = new LiveSession(hostConnectionId);
            _roles.AddHost(hostConnectionId, code.Value);
            return true;
        }
    }

    /// <summary>
    /// Records a connection as waiting on the DM's decision. It is NOT routed into the session's
    /// traffic by this, and receives nothing until <see cref="TryAdmit"/> (R-1.3b).
    /// </summary>
    /// <remarks>
    /// The gate D-13 requires. A pending connection that received session payloads it could not
    /// decrypt would still learn a session is live, its cadence, and roughly how much is happening —
    /// inference from what it does receive, which D-13 forbids in terms. Encryption does not
    /// substitute for not sending.
    /// </remarks>
    /// <param name="code">The session being joined.</param>
    /// <param name="connectionId">The waiting connection.</param>
    /// <param name="joinerPublicKey">The key the joiner presented, which is what names it later.</param>
    /// <returns><c>false</c> if no session is live under that code.</returns>
    public bool TryRegisterPending(string code, string connectionId, byte[] joinerPublicKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);
        ArgumentNullException.ThrowIfNull(joinerPublicKey);

        lock (_gate)
        {
            if (!_byCode.TryGetValue(code, out var session))
            {
                return false;
            }

            session.Pending[Convert.ToBase64String(joinerPublicKey)] = connectionId;
            _roles.Add(connectionId, code);
            return true;
        }
    }

    /// <summary>
    /// Promotes a pending joiner to a member, after which — and only after which — it is routed into
    /// the session's traffic.
    /// </summary>
    /// <returns>
    /// <c>false</c> if nobody is pending under that key, so a stale, replayed or invented decision
    /// admits nobody rather than admitting whoever happens to be next.
    /// </returns>
    public bool TryAdmit(string code, byte[] joinerPublicKey, [NotNullWhen(true)] out string? connectionId) =>
        TryResolvePending(code, joinerPublicKey, admit: true, out connectionId);

    /// <summary>
    /// Drops a pending joiner the host refused or let lapse. It never becomes a member, and the
    /// caller closes its connection once the answer is delivered (R-1.3b).
    /// </summary>
    public bool TryDeny(string code, byte[] joinerPublicKey, [NotNullWhen(true)] out string? connectionId) =>
        TryResolvePending(code, joinerPublicKey, admit: false, out connectionId);

    /// <summary>
    /// The connection waiting under <paramref name="joinerPublicKey"/>, <b>without resolving it</b>.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="TryAdmit"/> or <see cref="TryDeny"/> with a flag. Both of those
    /// <b>remove</b> the pending entry, because both answer the request. A pending notice answers
    /// nothing — it carries the host's key to a joiner who is still waiting (R-1.3a-i) — so routing
    /// it must leave the gate exactly as it found it. A lookup that mutated would admit or strand
    /// the joiner as a side effect of telling them something.
    /// </remarks>
    public bool TryGetPending(string code, byte[] joinerPublicKey, [NotNullWhen(true)] out string? connectionId)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);

        lock (_gate)
        {
            connectionId = null;
            if (!_byCode.TryGetValue(code, out var session)
                || !session.Pending.TryGetValue(Convert.ToBase64String(joinerPublicKey), out var pending))
            {
                return false;
            }

            connectionId = pending;
            return true;
        }
    }

    /// <summary>The connection hosting <paramref name="code"/>, if a session is live under it.</summary>
    public bool TryGetHost(string code, [NotNullWhen(true)] out string? hostConnectionId)
    {
        lock (_gate)
        {
            if (_byCode.TryGetValue(code, out var session))
            {
                hostConnectionId = session.HostConnectionId;
                return true;
            }

            hostConnectionId = null;
            return false;
        }
    }

    /// <summary>Whether this connection is in this session at all, in any role including pending.</summary>
    public bool IsParticipant(string code, string connectionId)
    {
        lock (_gate)
        {
            return _roles.IsIn(connectionId, code);
        }
    }

    /// <summary>
    /// Whether this connection has been admitted to this session, as opposed to merely waiting.
    /// Pending is not admitted, and only admitted is routed.
    /// </summary>
    public bool IsMember(string code, string connectionId)
    {
        lock (_gate)
        {
            return _byCode.TryGetValue(code, out var session) && session.IsMember(connectionId);
        }
    }

    /// <summary>
    /// Everyone admitted to <paramref name="code"/> except <paramref name="excludedConnectionId"/> —
    /// the recipients of a forwarded payload. Pending connections are never in this list.
    /// </summary>
    public IReadOnlyList<string> MembersExcept(string code, string excludedConnectionId)
    {
        lock (_gate)
        {
            if (!_byCode.TryGetValue(code, out var session))
            {
                return [];
            }

            var recipients = new List<string>(session.Members.Count + 1);
            foreach (var member in session.Everyone())
            {
                if (!string.Equals(member, excludedConnectionId, StringComparison.Ordinal))
                {
                    recipients.Add(member);
                }
            }

            return recipients;
        }
    }

    /// <summary>
    /// Drops a connection from <b>every</b> session it was part of. If it hosted one, that session
    /// ends and its code returns to the pool at once — the relay holds no grace window, because a
    /// grace window is session state and the relay is not authoritative (D-3; R-1.4 belongs to the
    /// DM's client).
    /// </summary>
    /// <remarks>
    /// Total over the role set by construction: it iterates every code the connection touched, and
    /// within each one clears the host slot, membership and <b>all</b> pending entries naming it.
    /// Anything less leaves a dead id somewhere nothing removes it from.
    /// </remarks>
    public ConnectionRemoval Remove(string connectionId)
    {
        lock (_gate)
        {
            if (_roles.Forget(connectionId) is not { } codes)
            {
                return ConnectionRemoval.NotInSession;
            }

            var departures = new List<SessionDeparture>(codes.Count);
            foreach (var code in codes)
            {
                if (!_byCode.TryGetValue(code, out var session))
                {
                    continue;
                }

                departures.Add(session.IsHost(connectionId)
                    ? EndSession(code, session, connectionId)
                    : LeaveSession(code, session, connectionId));
            }

            return new ConnectionRemoval(departures);
        }
    }

    private SessionDeparture EndSession(string code, LiveSession session, string hostConnectionId)
    {
        _byCode.Remove(code);

        var orphaned = session.Everyone()
            .Concat(session.Pending.Values)
            .Where(id => !string.Equals(id, hostConnectionId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Detach them from THIS code only. An orphan may still be hosting or joined elsewhere, and
        // wiping its whole role set here is how ending one session would strand another.
        foreach (var orphan in orphaned)
        {
            _roles.Remove(orphan, code);
        }

        return new SessionDeparture(code, EndedSession: true, orphaned);
    }

    private static SessionDeparture LeaveSession(string code, LiveSession session, string connectionId)
    {
        // TAKEN BEFORE THE REMOVAL, because after it there is nothing left to name them by. Null
        // when the departing connection was only ever pending here -- a joiner that never got in
        // is not a member whose seat the host is holding (A-1.28, R-1.5a).
        var departedKey = session.Members.TryGetValue(connectionId, out var key) ? key : null;

        session.Members.Remove(connectionId);
        session.ForgetAllPending(connectionId);
        return new SessionDeparture(code, EndedSession: false, [], session.HostConnectionId, departedKey);
    }

    private bool TryResolvePending(
        string code,
        byte[] joinerPublicKey,
        bool admit,
        [NotNullWhen(true)] out string? connectionId)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);

        lock (_gate)
        {
            connectionId = null;
            if (!_byCode.TryGetValue(code, out var session)
                || !session.Pending.Remove(Convert.ToBase64String(joinerPublicKey), out var pending))
            {
                return false;
            }

            if (admit)
            {
                session.Members[pending] = Convert.ToBase64String(joinerPublicKey);
            }
            else if (!session.IsMember(pending) && !session.HasPending(pending))
            {
                // Refused, and holding no other role here, so it leaves this session entirely.
                _roles.Remove(pending, code);
            }

            connectionId = pending;
            return true;
        }
    }
}
