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
    /// <param name="window">
    /// How long a session survives an interruption (A-1.23, A-1.27). <b>ONE value feeds BOTH
    /// clocks</b>, which is what A-1.27 asks for, and it is a required parameter rather than an
    /// optional one on purpose.
    /// <para>
    /// <b>No default, and that is the whole point of this parameter.</b> It was
    /// <c>TimeSpan? seatWindow = null</c>, which fell through to <see cref="GraceWindow.Default"/> —
    /// and a defaulted parameter nobody supplies is the same silence as an omitted argument. Both
    /// windows then read the same literal and AGREED, so any test asserting they matched would have
    /// passed while A-1.27's third clause — <i>neither is a literal</i> — was still false.
    /// <b>De-duplicating a literal is not single-sourcing it to a setting.</b>
    /// </para>
    /// <para>
    /// Read once, here. Changing the setting takes effect for the next session rather than the
    /// running one, because <see cref="GraceWindow"/> fixes its length at construction. A-1.23 asks
    /// for settable, not live.
    /// </para>
    /// </param>
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

    /// <summary>How long this client holds a session after losing the host (R-1.4).</summary>
    public GraceWindow Grace { get; }

    /// <summary>
    /// How long this client's own seat stays resumable after its link drops (R-1.5a, BUG-53).
    /// </summary>
    /// <remarks>
    /// <b>A different clock from <see cref="Grace"/>, measuring a different thing.</b> Grace is what
    /// this client allows a lost HOST; this is how long this client's own seat is worth waiting on
    /// before it should behave as though the session is over. The same <see cref="GraceWindow"/>
    /// type serves both because the mechanism — start, tick, expire — is identical; only the
    /// subject differs, and the doc on each says which.
    /// </remarks>
    public GraceWindow Seat { get; }

    /// <summary>
    /// Whether this client is in a joined session, INCLUDING one whose link has dropped but whose
    /// seat could still be resumed (R-1.3h, BUG-53, A-1.17a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Exclusivity ends when the SEAT ends, never when the connection does.</b> An admitted
    /// joiner whose link drops is still a member — the DM is still holding their place — so offering
    /// them a host affordance the instant the link falls is R-1.3h violated in the commonest failure
    /// there is, a network hiccup.
    /// </para>
    /// <para>
    /// <b>Not keyed on <see cref="JoinPhase.Failed"/>, which cannot carry this.</b> Four different
    /// predecessors reach that phase — never got in, asked and was never answered, keys could not be
    /// made, and WAS IN and dropped — and only the last holds a seat. A blanket <c>Failed</c> would
    /// also contradict <see cref="JoinAttempt.MayRequestAgain"/>, which treats it as retryable, and
    /// would lock a user out of hosting after a join that never succeeded.
    /// </para>
    /// <para>
    /// <b>Nor on the session key.</b> It answers <i>was admitted</i> and says nothing about when the
    /// seat lapses, and keying membership on holding a derived key conflates <i>can decrypt</i> with
    /// <i>is a member</i>. The seat clock is the thing that expires, so the seat clock is the thing
    /// asked.
    /// </para>
    /// </remarks>
    public bool InAJoinedSession =>
        _join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted
        || Seat.IsRunning;

    /// <summary>Ends the seat, because this client is deliberately starting again.</summary>
    /// <remarks>
    /// R-1.5a: a deliberate quit removes the seat immediately. Asking to join again is that, so the
    /// suppression lifts at once rather than waiting out a window nobody is using.
    /// </remarks>
    public void SeatReleased() => Seat.Reset();

    /// <summary>
    /// Advances both windows. Returns whether the GRACE window expired, which ends a hosted session.
    /// </summary>
    /// <remarks>
    /// The seat's expiry ends nothing by itself — it only stops suppressing the host affordance, and
    /// <see cref="InAJoinedSession"/> reads that directly. <b>Both halves are required:</b>
    /// suppression without expiry would lock a user out of hosting forever, which is worse than the
    /// bug it fixes.
    /// </remarks>
    /// <param name="sinceLastTick">Elapsed time since the previous call.</param>
    public bool Tick(TimeSpan sinceLastTick)
    {
        Seat.Tick(sinceLastTick);
        return Grace.Tick(sinceLastTick);
    }

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

        // BUG-53. Started BEFORE the phase moves, because Admitted is the only predecessor that
        // holds a seat and the phase is about to stop saying so. GraceWindow's method is named for
        // its first caller; what it means here is "the thing we were waiting on went away, start
        // counting".
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
