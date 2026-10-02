using System;

namespace DungeonMasterXIV.Net;

/// <summary>Starts and abandons a join request, making fresh joiner keys and releasing the old seat and keys.</summary>
internal sealed class JoinRequester
{
    private readonly OutboundHandshake _handshake;
    private readonly SessionInterruption _interruption;
    private readonly JoinAttempt _join;
    private readonly Func<SessionKeyExchange> _newKeys;
    private readonly Action _synchronise;

    public JoinRequester(
        OutboundHandshake handshake,
        SessionInterruption interruption,
        JoinAttempt join,
        Func<SessionKeyExchange> newKeys,
        Action synchronise)
    {
        ArgumentNullException.ThrowIfNull(handshake);
        ArgumentNullException.ThrowIfNull(interruption);
        ArgumentNullException.ThrowIfNull(join);
        ArgumentNullException.ThrowIfNull(newKeys);
        ArgumentNullException.ThrowIfNull(synchronise);

        _handshake = handshake;
        _interruption = interruption;
        _join = join;
        _newKeys = newKeys;
        _synchronise = synchronise;
    }

    public SessionKeyExchange? Keys { get; private set; }

    public byte[]? SessionKey { get; internal set; }

    public void Request(SessionCode code, DisplayName name, Guid? claimedParticipantId)
    {
        _handshake.JoiningAs(name, claimedParticipantId);

        ReleaseTheSeatAndKeys();

        if (!SessionKeyPair.TryMake(_newKeys, out var joinerKeys))
        {
            _join.Fail(SessionFailure.SessionKeysUnavailable);
            return;
        }

        Keys = joinerKeys;
        _join.Request(code);

        _handshake.ForgetJoinRequest();
        _synchronise();
    }

    public void Left()
    {
        ReleaseTheSeatAndKeys();
        _join.Left();

        _synchronise();
    }

    private void ReleaseTheSeatAndKeys()
    {
        _interruption.JoinReleased();
        Keys?.Dispose();
        Keys = null;
        SessionKey = null;
    }
}
