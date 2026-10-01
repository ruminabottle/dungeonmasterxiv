using System;

namespace DungeonMasterXIV.Net;

/// <summary>Handles lost relay connections, running the host and seat grace windows or failing the session.</summary>
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
        Seat.Tick(sinceLastTick);
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

    public void Fail(SessionFailure failure)
    {
        if (failure == SessionFailure.ConnectionLost && _host.Phase == HostingPhase.Hosting)
        {
            Grace.HostLost();
            return;
        }

        if (_host.Phase is HostingPhase.Registering or HostingPhase.Hosting)
        {
            _host.Fail(failure);
        }

        if (_join.Phase == JoinPhase.Admitted)
        {
            Seat.HostLost();
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
