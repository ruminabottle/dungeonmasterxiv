using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// When an admission request stops being answerable. An <b>absolute instant</b>, decided once by the
/// DM's client and carried to the joiner.
/// </summary>
/// <remarks>
/// <para>
/// R-1.3c requires the admission wait and R-1.3a's prompt expiry to be the same window seen from two
/// sides, and forbids them drifting apart. <b>When two parties must agree about time, one decides
/// and the other is told.</b> A duration each side counts independently is two clocks pretending to
/// be one: they diverge on network delay, clock skew, or a client the OS suspended, and the
/// divergence produces exactly the failure R-1.3c names — a player told the request lapsed while the
/// DM still holds a live prompt, so the DM accepts into nothing and neither side sees a bug.
/// </para>
/// <para>
/// This type exists so a duration is not representable. There is no constructor taking a
/// <see cref="TimeSpan"/>, and <see cref="RemainingAt"/> takes the current instant as a parameter
/// rather than reading a clock — the same rule the rest of Core follows, and what keeps a countdown
/// testable without waiting for one.
/// </para>
/// </remarks>
public readonly struct AdmissionDeadline : IEquatable<AdmissionDeadline>
{
    /// <summary>
    /// How long a DM has to answer, per R-1.3a. Deliberately generous — a DM mid-encounter should
    /// not lose a request because they were busy — and R-1.3a is explicit that what matters is the
    /// window being bounded at all, not that it is short.
    /// </summary>
    /// <remarks>
    /// R-1.3a pairs this with the 11-character fingerprint: if the expiry is ever removed, the
    /// fingerprint must go to 14 characters. Do not change one without the other.
    /// </remarks>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private AdmissionDeadline(long utcTicks) => UtcTicks = utcTicks;

    /// <summary>The instant itself, as UTC ticks — the form that travels on the wire.</summary>
    public long UtcTicks { get; }

    /// <summary>The instant as a <see cref="DateTimeOffset"/>.</summary>
    public DateTimeOffset Instant => new(UtcTicks, TimeSpan.Zero);

    /// <summary>
    /// Decides a deadline. Called <b>once, by the DM's client</b>, which is authoritative under D-3.
    /// A joining client never calls this — it is told.
    /// </summary>
    /// <param name="decidedAt">The instant the DM's client received the request.</param>
    public static AdmissionDeadline DecidedByHost(DateTimeOffset decidedAt) =>
        new(decidedAt.Add(Window).ToUniversalTime().Ticks);

    /// <summary>Rebuilds a deadline received from the wire.</summary>
    public static AdmissionDeadline FromWire(long utcTicks) => new(utcTicks);

    /// <summary>
    /// How long is left at <paramref name="now"/>, floored at zero so a countdown never runs
    /// negative. R-1.3c requires the joining player to see the wait is bounded <i>while it is
    /// happening</i>, which is what this is for.
    /// </summary>
    public TimeSpan RemainingAt(DateTimeOffset now)
    {
        var remaining = Instant - now.ToUniversalTime();
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    /// <summary>Whether the window has closed at <paramref name="now"/>.</summary>
    public bool HasLapsedAt(DateTimeOffset now) => RemainingAt(now) == TimeSpan.Zero;

    /// <inheritdoc />
    public bool Equals(AdmissionDeadline other) => UtcTicks == other.UtcTicks;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AdmissionDeadline other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => UtcTicks.GetHashCode();
}
