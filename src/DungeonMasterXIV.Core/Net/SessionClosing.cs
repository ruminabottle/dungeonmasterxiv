using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// When a session the DM has deliberately ended actually closes (R-1.3g, A-1.16). An
/// <b>absolute instant</b>, decided once by the DM's client and carried to every participant.
/// </summary>
/// <remarks>
/// <para>
/// <b>R-1.3g's countdown is a requirement, not a courtesy</b> — <i>"the session is closing" without
/// "how long remains" is the indefinite-wait failure R-1.3c and R-1.8 both exist to forbid.</i> So
/// this type exists to make the second half unskippable: there is no way to announce a closure
/// without carrying when it happens.
/// </para>
/// <para>
/// <b>An instant, and a duration is deliberately not representable</b> — the same rule and the same
/// reason as <see cref="AdmissionDeadline"/>, which this mirrors on purpose rather than by
/// coincidence. When two parties must agree about time, one decides and the other is told; a
/// duration each side counts independently is two clocks pretending to be one, and they diverge on
/// network delay, clock skew, or a client the OS suspended. A participant told "two minutes" at the
/// moment of sending is told something already false by the time it arrives.
/// </para>
/// <para>
/// <b>A SIBLING RATHER THAN A REUSE.</b> <see cref="AdmissionDeadline"/> has the same shape and a
/// different meaning — it bounds how long a DM has to ANSWER, this bounds how long a session has
/// left to LIVE. Reusing that type here would put one name on two requirements, and R-1.3a's window
/// could then not be changed without silently changing R-1.3g's.
/// </para>
/// <para>
/// <b>IT CONTAINS NO DURATION OF ITS OWN, AND THAT IS THE POINT.</b> <see cref="AdmissionDeadline"/>
/// carries R-1.3a's fifteen minutes because R-1.3a names it. R-1.3g names no number, so this type
/// takes the instant from its caller rather than deciding one — the window a closing session gets is
/// a product question, and a literal here would answer it silently and permanently. If a number ever
/// belongs to R-1.3g it belongs beside the configured interruption window, not inside this struct.
/// </para>
/// </remarks>
public readonly struct SessionClosing : IEquatable<SessionClosing>
{
    private SessionClosing(long utcTicks) => UtcTicks = utcTicks;

    /// <summary>The instant itself, as UTC ticks — the form that travels on the wire.</summary>
    public long UtcTicks { get; }

    /// <summary>The instant as a <see cref="DateTimeOffset"/>.</summary>
    public DateTimeOffset Instant => new(UtcTicks, TimeSpan.Zero);

    /// <summary>
    /// Decides when this session closes. Called <b>once, by the DM's client</b>, which is
    /// authoritative under D-3. A joining client never calls this — it is told.
    /// </summary>
    /// <param name="closesAt">
    /// When the session stops. Supplied by the caller, which is the only place the configured
    /// window is known — see the remark on why this type holds no duration.
    /// </param>
    public static SessionClosing DecidedByHost(DateTimeOffset closesAt) =>
        new(closesAt.ToUniversalTime().Ticks);

    /// <summary>
    /// Rebuilds a closing instant received from the wire, or null if the value cannot be one.
    /// </summary>
    /// <remarks>
    /// Returns null rather than throwing, and there is no unvalidated construction path, for
    /// <see cref="AdmissionDeadline.TryFromWire"/>'s reason: <see cref="Instant"/> throws outside
    /// <c>[0, DateTime.MaxValue.Ticks]</c>, and <see cref="RemainingAt"/> reads it in front of a
    /// participant watching a countdown. An out-of-range value from another client is not a bad
    /// number, it is a crash in a draw path.
    /// </remarks>
    /// <param name="utcTicks">The value that arrived.</param>
    public static SessionClosing? TryFromWire(long utcTicks) =>
        utcTicks >= 0 && utcTicks <= DateTime.MaxValue.Ticks ? new SessionClosing(utcTicks) : null;

    /// <summary>
    /// How long is left at <paramref name="now"/>, floored at zero so a countdown never runs
    /// negative.
    /// </summary>
    /// <param name="now">The current instant, passed in rather than read, so a countdown is testable.</param>
    public TimeSpan RemainingAt(DateTimeOffset now)
    {
        var remaining = Instant - now.ToUniversalTime();
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>Whether the session has already closed at <paramref name="now"/>.</summary>
    /// <param name="now">The current instant.</param>
    public bool HasClosedAt(DateTimeOffset now) => RemainingAt(now) == TimeSpan.Zero;

    /// <inheritdoc />
    public bool Equals(SessionClosing other) => UtcTicks == other.UtcTicks;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SessionClosing other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => UtcTicks.GetHashCode();

    /// <summary>Whether two closings are the same instant.</summary>
    public static bool operator ==(SessionClosing left, SessionClosing right) => left.Equals(right);

    /// <summary>Whether two closings are different instants.</summary>
    public static bool operator !=(SessionClosing left, SessionClosing right) => !left.Equals(right);
}
