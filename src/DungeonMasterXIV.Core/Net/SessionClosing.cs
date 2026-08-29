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
/// left to LIVE. Reusing that type here would put one name on two requirements, and R-1.3l's window
/// could then not be changed without silently changing R-1.3g's.
/// </para>
    /// <para>
    /// <b>IT CARRIES R-1.3g's SIXTY SECONDS BECAUSE R-1.3g NOW NAMES THEM.</b> It did not, and this
    /// type deliberately held no duration while the question was open — a literal would have answered
    /// a product question silently. The Product Owner has since ruled the window, so the number
    /// belongs here on its own authority: R-1.3g names it, so the type expressing R-1.3g holds it.
    /// </para>
    /// <para>
    /// <b>A CITATION CORRECTED RATHER THAN RENAMED.</b> This paragraph used to justify itself by
    /// saying <see cref="AdmissionDeadline"/> carries "R-1.3a's fifteen minutes". <b>R-1.3a is about
    /// comparing a fingerprint and has never mentioned time at all.</b> The fifteen minutes has a home
    /// at <b>R-1.3l</b> as of 2026-08-29 — and that is not where it moved to, it is where it was
    /// FIRST WRITTEN DOWN. The old reference was not a stale pointer; it pointed at a requirement that
    /// never said it, and three places in the PRD cited it that way, each anchored and none followed
    /// back. <b>Three citations of one requirement is not corroboration — it is one unchecked claim
    /// with copies.</b>
    /// </para>
    /// <para>
    /// <b>DO NOT DERIVE ARITHMETIC FROM THE FIFTEEN MINUTES.</b> R-1.3l says in terms that it gives an
    /// already-decided value an OWNER rather than ruling it, and whether it was ever ruled is an open
    /// question. Sixty seconds here is ruled and safe to build on; that number is not.
    /// </para>
    /// <para>
    /// <b>NOT CONFIGURABLE, and that is the ruling rather than an omission.</b> The window's job is
    /// TIME TO NOTICE — long enough to survive a glance away, short enough that nobody sits in a room
    /// that is over, and <b>a round number a DM can say out loud</b>, so "you have a minute" and the
    /// software agree rather than contradicting the person running the game. A DM winding up a long
    /// session already controls the length of the goodbye by choosing when to press close; this
    /// window is not the goodbye, it is the confirmation that the goodbye happened.
    /// </para>
    /// <para>
    /// <b>AND THE HAZARD THE FIXED VALUE CREATED, WHICH IS WHY <see cref="DecidedByHost"/> TAKES THE
    /// MOMENT OF ENDING AND NOT A DEADLINE (A-1.16b).</b> Sixty seconds on the host and sixty on each
    /// client would display the same countdown everywhere and <b>pass A-1.16 while nothing was sent
    /// at all</b> — then drift, because the two clocks start at different instants. A CONFIGURABLE
    /// window HAD to travel in order to be observed, so the criterion policed the mechanism by
    /// accident; a constant does not, so the test must do it deliberately. <b>A participant never
    /// constructs one of these — it opens the instant it was SENT, through
    /// <see cref="TryFromWire"/>.</b>
    /// </para>
/// </remarks>
public readonly struct SessionClosing : IEquatable<SessionClosing>
{
    /// <summary>
    /// How long a session stays visible after the DM ends it, per R-1.3g. Sixty seconds, not
    /// configurable.
    /// </summary>
    /// <remarks>
    /// <b>This is NOT R-1.4's grace window and the two must not be reconciled.</b> R-1.4 is time for
    /// an UNREACHABLE host to come back; a deliberate quit has no coming back, and this is time to
    /// NOTICE. Different event, different purpose — and R-1.4's number sitting in the same PRD makes
    /// it the default nobody would think to argue about, which is why this says so here.
    /// </remarks>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private SessionClosing(long utcTicks) => UtcTicks = utcTicks;

    /// <summary>The instant itself, as UTC ticks — the form that travels on the wire.</summary>
    public long UtcTicks { get; }

    /// <summary>The instant as a <see cref="DateTimeOffset"/>.</summary>
    public DateTimeOffset Instant => new(UtcTicks, TimeSpan.Zero);

    /// <summary>
    /// Decides when this session closes. Called <b>once, by the DM's client</b>, which is
    /// authoritative under D-3. A joining client never calls this — it is told.
    /// </summary>
    /// <param name="endedAt">The instant the DM ended the session. <see cref="Window"/> is added here.</param>
    /// <remarks>
    /// <b>It takes the moment of ENDING, not a deadline, so no caller can supply a different
    /// window.</b> A caller that could choose one would be a second place the sixty seconds lives,
    /// and two places is how a host and a client come to disagree — the drift A-1.16b catches.
    /// </remarks>
    public static SessionClosing DecidedByHost(DateTimeOffset endedAt) =>
        new(endedAt.Add(Window).ToUniversalTime().Ticks);

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
