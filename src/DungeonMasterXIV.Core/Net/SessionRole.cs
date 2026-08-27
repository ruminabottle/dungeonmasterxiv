namespace DungeonMasterXIV.Net;

/// <summary>
/// What a participant may do. Taken from Foundry core via E-11.
/// </summary>
/// <remarks>
/// The distinction E-11 draws is that <b>an Assistant runs the table; only the DM controls who is at
/// it.</b> So an Assistant can drive an encounter, but ending the session, admitting or removing a
/// participant, and approving a relink stay with the DM — which is why admission logic in this chunk
/// is DM-only and does not branch on role.
/// </remarks>
public enum SessionRole
{
    /// <summary>A participant. Sends their own events, renders what the host sends.</summary>
    Player = 0,

    /// <summary>Runs the table — encounters, rolls for DM-controlled combatants. Not the door.</summary>
    Assistant = 1,

    /// <summary>Hosts the session and is the sole author of shared state (D-3).</summary>
    DungeonMaster = 2,
}
