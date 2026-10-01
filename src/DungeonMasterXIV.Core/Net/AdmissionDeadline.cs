using System;

namespace DungeonMasterXIV.Net;

public readonly struct AdmissionDeadline : IEquatable<AdmissionDeadline>
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private AdmissionDeadline(long utcTicks) => UtcTicks = utcTicks;

    public long UtcTicks { get; }

    public DateTimeOffset Instant => new(UtcTicks, TimeSpan.Zero);

    public static AdmissionDeadline DecidedByHost(DateTimeOffset decidedAt) =>
        new(decidedAt.Add(Window).ToUniversalTime().Ticks);

    public static AdmissionDeadline? TryFromWire(long utcTicks) =>
        utcTicks >= 0 && utcTicks <= DateTime.MaxValue.Ticks ? new AdmissionDeadline(utcTicks) : null;

    public TimeSpan RemainingAt(DateTimeOffset now)
    {
        var remaining = Instant - now.ToUniversalTime();
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    public bool HasLapsedAt(DateTimeOffset now) => RemainingAt(now) == TimeSpan.Zero;

    public bool Equals(AdmissionDeadline other) => UtcTicks == other.UtcTicks;

    public override bool Equals(object? obj) => obj is AdmissionDeadline other && Equals(other);

    public override int GetHashCode() => UtcTicks.GetHashCode();
}
