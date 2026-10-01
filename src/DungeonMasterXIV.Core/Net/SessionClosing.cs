using System;

namespace DungeonMasterXIV.Net;

/// <summary>The UTC instant a session closes, sixty seconds after the host ends it.</summary>
public readonly struct SessionClosing : IEquatable<SessionClosing>
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private SessionClosing(long utcTicks) => UtcTicks = utcTicks;

    public long UtcTicks { get; }

    public DateTimeOffset Instant => new(UtcTicks, TimeSpan.Zero);

    public static SessionClosing DecidedByHost(DateTimeOffset endedAt) =>
        new(endedAt.Add(Window).ToUniversalTime().Ticks);

    public static SessionClosing? TryFromWire(long utcTicks) =>
        utcTicks >= 0 && utcTicks <= DateTime.MaxValue.Ticks ? new SessionClosing(utcTicks) : null;

    public TimeSpan RemainingAt(DateTimeOffset now)
    {
        var remaining = Instant - now.ToUniversalTime();
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public bool HasClosedAt(DateTimeOffset now) => RemainingAt(now) == TimeSpan.Zero;

    public bool Equals(SessionClosing other) => UtcTicks == other.UtcTicks;

    public override bool Equals(object? obj) => obj is SessionClosing other && Equals(other);

    public override int GetHashCode() => UtcTicks.GetHashCode();

    public static bool operator ==(SessionClosing left, SessionClosing right) => left.Equals(right);

    public static bool operator !=(SessionClosing left, SessionClosing right) => !left.Equals(right);
}
