namespace DungeonMasterXIV.Net;

public enum JoinPhase
{
    Idle = 0,

    Contacting = 1,

    AwaitingDecision = 2,

    Admitted = 3,

    Denied = 4,

    Failed = 5,

    Lapsed = 6,
}
