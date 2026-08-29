using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// How long this client has been in its current phase, and what expires because of it (A-1.5b).
/// </summary>
/// <remarks>
/// <para>
/// <b>A stopwatch over two phase machines, and it was never the coordinator's state.</b>
/// <see cref="SessionCoordinator"/>'s own summary says it is for <i>whether a connection should
/// exist</i>; three fields tracking how long a phase has run, read by exactly two expiry calls, are
/// a different question that happened to live in the method that advances them.
/// </para>
/// <para>
/// <b>The reset rule is the whole of it, and it is easy to get subtly wrong.</b> The clock restarts
/// when EITHER phase moves, and on that tick nothing is checked for expiry — a phase that has just
/// begun cannot have timed out. In the coordinator that was a bare <c>return</c> in the middle of
/// <c>Tick</c>, sitting directly below a different <c>return</c> that meant something else
/// entirely: <i>hosting has stopped, abandon the frame</i>. <b>Two returns, one method, two
/// meanings</b> — separating them is most of the reason this type is worth having.
/// </para>
/// <para>
/// <b>Clock-free, like everything else in Core.</b> The caller supplies the delta, so nothing here
/// reads a clock and every timeout stays drivable from a test with an explicit
/// <see cref="TimeSpan"/> (R-1.3c).
/// </para>
/// </remarks>
internal sealed class PhaseTimeouts
{
    private TimeSpan _timeInPhase;
    private HostingPhase _hostPhase = HostingPhase.NotHosting;
    private JoinPhase _joinPhase = JoinPhase.Idle;

    /// <summary>
    /// Advances the clock and expires whatever ran out, reporting whether anything did.
    /// </summary>
    /// <param name="sinceLastTick">Elapsed time since the previous call.</param>
    /// <param name="host">The hosting phase machine, asked whether registration timed out.</param>
    /// <param name="join">The joining phase machine, asked whether contact timed out.</param>
    /// <param name="registrationWasSent">
    /// Whether this client's code request ever left. <b>It is the difference between "the relay
    /// heard us and said nothing" and "we never reached the relay"</b> (BUG-38), and it is passed in
    /// rather than read because the handshake is what knows it.
    /// </param>
    /// <returns>
    /// Whether a phase expired on this call — which the caller uses to bring the socket back into
    /// line, because an expiry can end the only thing that wanted a connection.
    /// </returns>
    public bool Advance(
        TimeSpan sinceLastTick,
        HostSession host,
        JoinAttempt join,
        bool registrationWasSent)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(join);

        // A phase that has just begun cannot have timed out, so this frame only records where we
        // are. Returning FALSE rather than falling through is the behaviour the coordinator had.
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
