using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What happens when the link drops, and the window that follows (R-1.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="SessionCoordinator"/>, whose class was 416 lines against a 400
/// block.</b> The seam is not the line count: losing a connection is one subject with one hard rule
/// behind it — <b>a dropped link is the START of a window, not the end of a session</b> — and the
/// routing that decides which side of the session a failure lands on belongs beside the window it
/// may open instead.
/// </para>
/// <para>
/// <b>It decides which failures are survivable and reports the rest.</b> Phases, keys, admissions
/// and the outbound handshake stay with <see cref="SessionCoordinator"/> and arrive here as
/// collaborators; <see cref="RelayLink"/> owns the connection and merely reports that it broke.
/// </para>
/// <para>
/// <b>Nothing here decides WHO may resume</b>, only how long a window runs. R-1.5a is explicit that
/// those are two mechanisms and that conflating them is the failure it went through twice to avoid:
/// the key answers who, the window answers how long.
/// </para>
/// </remarks>
internal sealed class SessionInterruption
{
    private readonly RelayLink _link;
    private readonly HostSession _host;
    private readonly JoinAttempt _join;
    private readonly Action _synchronise;

    /// <summary>Wires the interruption handling to the state it reads.</summary>
    /// <param name="link">The connection. Reports failures; decides none of them.</param>
    /// <param name="host">The hosting half of the session.</param>
    /// <param name="join">The joining half of the session.</param>
    /// <param name="synchronise">Brings the socket back into line once a failure has been applied.</param>
    public SessionInterruption(RelayLink link, HostSession host, JoinAttempt join, Action synchronise)
    {
        _link = link;
        _host = host;
        _join = join;
        _synchronise = synchronise;
    }

    /// <summary>How long this client holds a session after losing the host (R-1.4).</summary>
    public GraceWindow Grace { get; } = new();

    /// <summary>
    /// The relay answered again after a drop and confirmed we still hold our code.
    /// </summary>
    public void HostReconnected()
    {
        Grace.HostReturned();
    }

    /// <summary>
    /// The relay answered again after a drop but refused the code — somebody claimed it while we
    /// were gone.
    /// </summary>
    /// <remarks>
    /// This is the gap R-1.4 opens and the relay cannot close: it frees a code the moment a host
    /// disconnects, while the grace window keeps the session alive for <see cref="GraceWindow.Default"/>. R-1.2a's
    /// regenerate-and-retry then hands us a different code, and without this the DM would carry on
    /// hosting under it while every player still holds the old one — nothing erroring, nothing
    /// looking wrong, and the session simply unjoinable.
    /// </remarks>
    public void HostReconnectedWithNewCode()
    {
        Grace.HostReturned();
        _host.CodeSuperseded(SessionCodeGenerator.Next());
    }

    /// <summary>Reports a transport failure against whichever side of the session is active.</summary>
    public void Fail(SessionFailure failure)
    {
        // R-1.4: losing the host is not the end of the session, it is the start of a grace window.
        // Clients hold their last state and show plainly that it is no longer live; only expiry
        // ends things. Treating a dropped connection as an immediate end is the "instant kick" the
        // product decision rules out.
        if (failure == SessionFailure.ConnectionLost && _host.Phase == HostingPhase.Hosting)
        {
            Grace.HostLost();
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
