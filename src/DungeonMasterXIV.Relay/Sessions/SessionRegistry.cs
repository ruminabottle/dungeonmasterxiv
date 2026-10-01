using System.Diagnostics.CodeAnalysis;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>Thread-safe record of live sessions by code: their hosts, pending joiners and admitted members.</summary>
public sealed class SessionRegistry
{
    private readonly Lock _gate = new();

    private readonly Dictionary<string, LiveSession> _byCode = new(StringComparer.Ordinal);

    private readonly ConnectionRoles _roles = new();

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

    public bool TryAdmit(string code, byte[] joinerPublicKey, [NotNullWhen(true)] out string? connectionId) =>
        TryResolvePending(code, joinerPublicKey, admit: true, out connectionId);

    public bool TryDeny(string code, byte[] joinerPublicKey, [NotNullWhen(true)] out string? connectionId) =>
        TryResolvePending(code, joinerPublicKey, admit: false, out connectionId);

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

    public bool IsParticipant(string code, string connectionId)
    {
        lock (_gate)
        {
            return _roles.IsIn(connectionId, code);
        }
    }

    public bool IsMember(string code, string connectionId)
    {
        lock (_gate)
        {
            return _byCode.TryGetValue(code, out var session) && session.IsMember(connectionId);
        }
    }

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

        foreach (var orphan in orphaned)
        {
            _roles.Remove(orphan, code);
        }

        return new SessionDeparture(code, EndedSession: true, orphaned);
    }

    private static SessionDeparture LeaveSession(string code, LiveSession session, string connectionId)
    {
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
                _roles.Remove(pending, code);
            }

            connectionId = pending;
            return true;
        }
    }
}
