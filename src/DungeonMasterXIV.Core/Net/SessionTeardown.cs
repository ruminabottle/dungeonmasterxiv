using System;

namespace DungeonMasterXIV.Net;

/// <summary>Ends a session at teardown: leaves, stops hosting, and detaches.</summary>
public static class SessionTeardown
{
    public static void EndSessionForTeardown(
        this SessionCoordinator coordinator,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        coordinator.Membership.Leave();
        coordinator.StopHosting(now);
        coordinator.Detach();
    }
}
