using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// How long a client holds a session open after losing the host (R-1.4).
/// </summary>
/// <remarks>
/// <para>
/// The product decision is <b>grace, then a clean end</b> — not an instant kick and not an indefinite
/// freeze. While it runs, clients hold their last known state and show plainly that it is no longer
/// live; when it expires the session ends and every client says so. What must never happen is stale
/// data shown as though it were current.
/// </para>
/// <para>
/// Elapsed time is a parameter, never read from a clock, so expiry is drivable from a test with an
/// explicit <see cref="TimeSpan"/> rather than by waiting two minutes.
/// </para>
/// </remarks>
public sealed class GraceWindow
{
    /// <summary>
    /// R-1.4's starting value, and deliberately settable — the right length is an empirical question
    /// nobody has answered.
    /// </summary>
    /// <remarks>
    /// <b>Changing this is not a local decision.</b> <see cref="TransportContract.IsKeepAliveSafeFor"/>
    /// refuses a window too short for the keepalive: below three keepalive intervals an ordinary lull
    /// between rolls trips host-loss detection and ends a live session mid-play.
    /// </remarks>
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(2);

    private readonly TimeSpan _length;
    private TimeSpan _elapsed;

    /// <param name="length">How long to hold. Defaults to <see cref="Default"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If the window is too short for the keepalive to survive an ordinary lull. Refusing here rather
    /// than clamping is deliberate: a silently shortened window produces sessions that end mid-play
    /// for no visible reason.
    /// </exception>
    public GraceWindow(TimeSpan? length = null)
    {
        _length = length ?? Default;

        if (!TransportContract.IsKeepAliveSafeFor(_length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                _length,
                "A grace window shorter than three keepalive intervals ends live sessions during an ordinary lull.");
        }
    }

    /// <summary>Whether the host is currently missing and the window is running.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Whether the window ran out and the session is over (R-1.4).</summary>
    public bool HasExpired { get; private set; }

    /// <summary>How long is left, floored at zero. Rendered so clients can see the wait is bounded.</summary>
    public TimeSpan Remaining => _length - _elapsed < TimeSpan.Zero ? TimeSpan.Zero : _length - _elapsed;

    /// <summary>The host went missing. Clients hold their last state and mark it not live.</summary>
    public void HostLost()
    {
        if (IsRunning || HasExpired)
        {
            return;
        }

        IsRunning = true;
        _elapsed = TimeSpan.Zero;
    }

    /// <summary>
    /// The host came back inside the window. Clients resync <b>from the host</b>, which remains
    /// authoritative (D-3); they never reconcile with each other.
    /// </summary>
    /// <returns>True if this was a recovery, false if there was nothing to recover from.</returns>
    public bool HostReturned()
    {
        if (!IsRunning)
        {
            return false;
        }

        IsRunning = false;
        _elapsed = TimeSpan.Zero;
        return true;
    }

    /// <summary>
    /// Advances the window. Returns true on the call that ends the session, so a caller can act on
    /// the transition rather than polling <see cref="HasExpired"/>.
    /// </summary>
    public bool Tick(TimeSpan sinceLastTick)
    {
        if (!IsRunning)
        {
            return false;
        }

        _elapsed += sinceLastTick;
        if (Remaining > TimeSpan.Zero)
        {
            return false;
        }

        IsRunning = false;
        HasExpired = true;
        return true;
    }

    /// <summary>Resets for a new session.</summary>
    public void Reset()
    {
        IsRunning = false;
        HasExpired = false;
        _elapsed = TimeSpan.Zero;
    }
}
