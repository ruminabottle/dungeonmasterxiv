using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Sessions;

/// <summary>Thread-safe record of live sessions by code: their hosts, pending joiners and admitted members.</summary>
public sealed class SessionRegistry
{
    private readonly Lock _gate = new();

    private readonly Dictionary<string, LiveSession> _byCode = new(StringComparer.Ordinal);

    private readonly ConnectionRoles _roles = new();

    private readonly TimeProvider _clock;

    public SessionRegistry(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

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

    public bool TryClaim(SessionCode code, string hostConnectionId, byte[]? reclaimHash = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostConnectionId);

        lock (_gate)
        {
            if (_byCode.ContainsKey(code.Value) || _roles.Hosts(hostConnectionId))
            {
                return false;
            }

            _byCode[code.Value] = new LiveSession(hostConnectionId, reclaimHash);
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
            if (_byCode.TryGetValue(code, out var session) && session.HostConnectionId is { } host)
            {
                hostConnectionId = host;
                return true;
            }

            hostConnectionId = null;
            return false;
        }
    }

    public bool IsHostAway(string code)
    {
        lock (_gate)
        {
            return _byCode.TryGetValue(code, out var session) && session.HostAway;
        }
    }

    public bool TryReclaim(
        SessionCode code,
        byte[] secret,
        string connectionId,
        out IReadOnlyList<string> members,
        out IReadOnlyList<string> droppedMemberKeys)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        lock (_gate)
        {
            members = [];
            droppedMemberKeys = [];
            if (!_byCode.TryGetValue(code.Value, out var session)
                || session.ReclaimHash is not { } expected
                || _roles.Hosts(connectionId)
                || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(secret), expected))
            {
                return false;
            }

            if (session.HostConnectionId is { } old && !string.Equals(old, connectionId, StringComparison.Ordinal))
            {
                _roles.Remove(old, code.Value);
            }

            session.HostConnectionId = connectionId;
            session.HostAwaySince = null;
            _roles.AddHost(connectionId, code.Value);
            members = session.Members.Keys.ToArray();
            droppedMemberKeys = session.DroppedWhileAway.ToArray();
            session.DroppedWhileAway.Clear();
            return true;
        }
    }

    public IReadOnlyList<SessionDeparture> ExpireHolds(TimeSpan hold)
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            var expired = _byCode
                .Where(entry => entry.Value.HostAwaySince is { } since && now - since >= hold)
                .Select(entry => entry.Key)
                .ToArray();

            var departures = new List<SessionDeparture>(expired.Length);
            foreach (var code in expired)
            {
                var session = _byCode[code];
                _byCode.Remove(code);

                var orphaned = session.Members.Keys.ToArray();
                foreach (var orphan in orphaned)
                {
                    _roles.Remove(orphan, code);
                }

                departures.Add(new SessionDeparture(code, EndedSession: true, orphaned));
            }

            return departures;
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
                    ? session.ReclaimHash is not null
                        ? HoldSession(code, session)
                        : EndSession(code, session, connectionId)
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

    private SessionDeparture HoldSession(string code, LiveSession session)
    {
        session.HostAwaySince = _clock.GetUtcNow();
        session.HostConnectionId = null;

        var waiting = session.Pending.Values.Distinct(StringComparer.Ordinal).ToArray();
        session.Pending.Clear();
        foreach (var joiner in waiting.Where(joiner => !session.Members.ContainsKey(joiner)))
        {
            _roles.Remove(joiner, code);
        }

        return new SessionDeparture(code, EndedSession: false, waiting, HeldMembers: session.Members.Keys.ToArray());
    }

    private static SessionDeparture LeaveSession(string code, LiveSession session, string connectionId)
    {
        var departedKey = session.Members.TryGetValue(connectionId, out var key) ? key : null;

        session.Members.Remove(connectionId);
        session.ForgetAllPending(connectionId);

        if (session.HostAway && departedKey is { } droppedKey)
        {
            session.DroppedWhileAway.Add(droppedKey);
        }

        return new SessionDeparture(code, EndedSession: false, [], session.HostConnectionId ?? string.Empty, departedKey);
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
