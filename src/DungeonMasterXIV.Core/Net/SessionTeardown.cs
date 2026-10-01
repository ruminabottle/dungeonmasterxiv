using System;

using DungeonMasterXIV.Data;

namespace DungeonMasterXIV.Net;

/// <summary>Ends a session at teardown: retains the log if asked, leaves, stops hosting, and detaches.</summary>
public static class SessionTeardown
{
    public static void EndSessionForTeardown(
        this SessionCoordinator coordinator,
        DateTimeOffset now,
        SessionLogRetention? retention = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        retention?.Retain(coordinator.InAHostedSession, now.UtcTicks);

        coordinator.Membership.Leave();
        coordinator.StopHosting(now);
        coordinator.Detach();
    }
}
