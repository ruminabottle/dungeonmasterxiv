using System;

namespace DungeonMasterXIV.Net;

/// <summary>Times how long hosting or joining has stayed in one phase and fails it when that runs too long.</summary>
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
