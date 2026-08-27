namespace DungeonMasterXIV.Net;

/// <summary>
/// Where a joining player is (R-1.3). Every value is something the UI can state plainly; R-1.3
/// requires the player always to know which one they are in and forbids an ambiguous spinner.
/// </summary>
public enum JoinPhase
{
    /// <summary>Not trying to join.</summary>
    Idle = 0,

    /// <summary>Reaching the relay with a code. Bounded by a timeout, never open-ended.</summary>
    Contacting = 1,

    /// <summary>The DM has the request and has not answered. Nothing flows in this state (R-1.3).</summary>
    AwaitingDecision = 2,

    /// <summary>The DM accepted. Session state may now flow.</summary>
    Admitted = 3,

    /// <summary>The DM declined. No session state flows, and none ever did.</summary>
    Denied = 4,

    /// <summary>The attempt failed before any decision. See <see cref="JoinAttempt.Failure"/>.</summary>
    Failed = 5,

    /// <summary>
    /// The window closed and the DM never answered (R-1.3c). Deliberately not
    /// <see cref="Denied"/>: nobody refused this player, so asking again is reasonable and the UI
    /// should say so. Telling someone they were refused when nobody looked is a different and worse
    /// message than telling them nobody looked.
    /// </summary>
    Lapsed = 6,
}
