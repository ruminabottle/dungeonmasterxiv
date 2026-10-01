using System;

namespace DungeonMasterXIV.Net;

internal sealed class PhaseTimeouts
{
    private TimeSpan _timeInPhase;
    private HostingPhase _hostPhase = HostingPhase.NotHosting;
    private JoinPhase _joinPhase = JoinPhase.Idle;

    public bool Advance(
        TimeSpan sinceLastTick,
        HostSession host,
        JoinAttempt join,
        bool registrationWasSent)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(join);

        if (host.Phase != _hostPhase || join.Phase != _joinPhase)
        {
            _hostPhase = host.Phase;
            _joinPhase = join.Phase;
            _timeInPhase = TimeSpan.Zero;
            return false;
        }

        _timeInPhase += sinceLastTick;

        var expired = host.ExpireIfRegistrationTimedOut(_timeInPhase, registrationWasSent);
        expired |= join.ExpireIfContactTimedOut(_timeInPhase);

        return expired;
    }
}
