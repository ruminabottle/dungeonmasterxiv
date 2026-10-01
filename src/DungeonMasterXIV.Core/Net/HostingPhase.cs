namespace DungeonMasterXIV.Net;

/// <summary>The stage of hosting: not hosting, registering a code, hosting, or failed.</summary>
public enum HostingPhase
{
    NotHosting = 0,

    Registering = 1,

    Hosting = 2,

    Failed = 3,
}
