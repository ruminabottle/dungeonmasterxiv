namespace DungeonMasterXIV.Net;

/// <summary>Where a DM's client is in the hosting lifecycle (R-1.1).</summary>
public enum HostingPhase
{
    /// <summary>No session, and no relay connection. R-1.1 requires these to be the same state.</summary>
    NotHosting = 0,

    /// <summary>Connecting to the relay and claiming a code. Transient, and bounded by a timeout.</summary>
    Registering = 1,

    /// <summary>The session is live and the relay has the code.</summary>
    Hosting = 2,

    /// <summary>Registering did not complete. See <see cref="HostSession.Failure"/>.</summary>
    Failed = 3,
}
