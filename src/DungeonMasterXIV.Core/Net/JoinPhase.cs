namespace DungeonMasterXIV.Net;

/// <summary>The stage of a join attempt, from idle through contacting and awaiting a decision to its outcome.</summary>
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
