using System;

namespace DungeonMasterXIV.Net;

/// <summary>Applies session failures, starting the host or seat grace window or failing hosting and joining.</summary>
internal sealed class SessionInterruption
{
    private readonly RelayLink _link;
    private readonly HostSession _host;
    private readonly JoinAttempt _join;
    private readonly Action _synchronise;

    public SessionInterruption(
        RelayLink link,
        HostSession host,
        JoinAttempt join,
        Action synchronise,
        TimeSpan window)
    {
        _link = link;
        _host = host;
        _join = join;
        _synchronise = synchronise;
        Grace = new GraceWindow(window);
        Seat = new GraceWindow(window);
    }

    public GraceWindow Grace { get; }

    public GraceWindow Seat { get; }

    public bool InAJoinedSession =>
        _join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted
        || Seat.IsRunning;

    public void SeatReleased() => Seat.Reset();

    public bool Tick(TimeSpan sinceLastTick)
    {
        if (Seat.IsRunning && _join.Phase != JoinPhase.Admitted)
        {
            Seat.Reset();
        }

        if (Seat.IsRunning && _join.Phase == JoinPhase.Admitted && !_join.Resuming)
        {
            Seat.HostReturned();
        }

        if (Seat.Tick(sinceLastTick))
        {
            _join.SeatExpired();
            _synchronise();
        }

        return Grace.Tick(sinceLastTick);
    }

    public void HostReconnected()
    {
        Grace.HostReturned();
    }

    public void HostReconnectedWithNewCode()
    {
        Grace.HostReturned();
        _host.CodeSuperseded(SessionCodeGenerator.Next());
    }

    public bool Reconnecting => Grace.IsRunning || Seat.IsRunning;

    public void Fail(SessionFailure failure)
    {
        var dropped = failure == SessionFailure.ConnectionLost
            || (failure == SessionFailure.RelayUnreachable && Reconnecting);

        if (dropped && _host.Phase == HostingPhase.Hosting)
        {
            Grace.HostLost();
            return;
        }

        if (dropped && _join.Phase == JoinPhase.Admitted)
        {
            Seat.HostLost();
            _join.StartResuming();
            return;
        }

        if (_host.Phase is HostingPhase.Registering or HostingPhase.Hosting)
        {
            _host.Fail(failure);
        }

        if (_join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted)
        {
            _join.Fail(failure);
        }

        _synchronise();
    }

    public void ApplyReportedFailure()
    {
        if (_link.TryTakeReportedFailure(out var failure))
        {
            Fail(failure);
        }
    }
}
