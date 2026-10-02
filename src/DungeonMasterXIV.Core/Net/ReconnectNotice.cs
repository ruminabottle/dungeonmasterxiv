using System;
using System.Globalization;

namespace DungeonMasterXIV.Net;

/// <summary>The one reconnecting line a client shows, only once a drop has outlasted the silent window.</summary>
public static class ReconnectNotice
{
    public static readonly TimeSpan Silent = TimeSpan.FromSeconds(10);

    public static string? For(bool hosting, GraceWindow grace, GraceWindow seat)
    {
        ArgumentNullException.ThrowIfNull(grace);
        ArgumentNullException.ThrowIfNull(seat);

        if (seat.IsRunning && seat.Elapsed >= Silent)
        {
            return $"Reconnecting… your seat is held for {Clock(seat.Remaining)}.";
        }

        if (!grace.IsRunning || grace.Elapsed < Silent)
        {
            return null;
        }

        return hosting
            ? $"Reconnecting to the relay… the session ends in {Clock(grace.Remaining)} if it doesn't come back."
            : $"The DM is reconnecting… the session ends in {Clock(grace.Remaining)} if they don't come back.";
    }

    private static string Clock(TimeSpan remaining) =>
        remaining.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
