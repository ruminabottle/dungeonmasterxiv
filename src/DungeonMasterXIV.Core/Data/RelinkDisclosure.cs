namespace DungeonMasterXIV.Data;

/// <summary>
/// What the player is shown about the participant ids their client stores, and what they are told
/// before deleting one (A-1.9b, A-1.9c).
/// </summary>
/// <remarks>
/// <para>
/// <b>ENGINEERING-AUTHORED UNDER R-1.7a's CONSTRAINTS (SQ-38, D-8, ruled SQ-67). NOT a placeholder,
/// and not product-ruled copy.</b> R-1.7a governs exactly the strings it QUOTES; this is not one of
/// them, so it is mine to write and it is the shipping text. <b>Anyone wanting to change it is
/// arguing with the constraints below, not filling in a blank.</b>
/// </para>
/// <para>
/// <b>The constraints, stated so a reviewer can check them rather than trust them.</b> Forbidden at
/// review: <i>"anonymous"</i>, <i>"private"</i>, <i>"we can't see anything"</i>, <i>"no one can see
/// your session"</i>, or any claim the relay cannot correlate sessions — each is false under D-8 and
/// the last is false even with encryption. And nothing may claim a session is protected when nobody
/// checked.
/// </para>
/// <para>
/// <b>In Core so the wording is under test rather than merely looked at</b>, which is the same reason
/// <c>AdmissionPrompt</c> holds the admission copy: a change of mind has to happen where a test is
/// watching.
/// </para>
/// </remarks>
public static class RelinkDisclosure
{
    /// <summary>What the stored ids are and why the client has them (A-1.9b).</summary>
    /// <remarks>
    /// <b>Explains the thing before offering to destroy it.</b> A-1.9b makes listing a precondition
    /// of deleting — <i>"you cannot meaningfully delete what you cannot see"</i> — and a list of
    /// opaque GUIDs is not seeing. This says what they are for, in the words of what the player
    /// actually did: they joined, and the DM let them in.
    /// </remarks>
    public const string WhatIsStored =
        "When a DM admits you, they create a participant for you in their campaign and tell your "
        + "client which one it is. Your client keeps that here, so the same DM can recognise you "
        + "when you join again with the same code.";

    /// <summary>The first, non-destructive step. Opens the warning rather than deleting.</summary>
    /// <remarks>
    /// <b>A-1.9c: a one-click irreversible delete FAILS, to the standard BUG-9 set.</b> BUG-9 was
    /// that the file a user understood LEAST was destroyed on one click while a readable one asked
    /// twice. A stored participant id is squarely in the first category — it is a number the player
    /// never chose and cannot read — so it gets the friction, not less of it.
    /// </remarks>
    public const string BeginForgetting = "Forget this";

    /// <summary>Backs out. Present so the warning is a QUESTION rather than a countdown.</summary>
    public const string KeepIt = "Keep it";

    /// <summary>Completes the deletion. Only reachable from the warning.</summary>
    public const string ConfirmForget = "Forget it permanently";

    /// <summary>
    /// What the player is told BEFORE the deletion completes (A-1.9c).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both facts A-1.9c names are here in order:</b> relink will no longer be possible — put in
    /// the words of what the player will see rather than in the product's vocabulary, because
    /// <i>"relink"</i> names a mechanism they have never been shown — and that they rejoin as a new
    /// participant needing fresh DM approval.
    /// </para>
    /// <para>
    /// <b>The third sentence is restraint, not decoration.</b> A-1.9e guarantees nothing is sent, and
    /// saying so is the difference between a player believing this is a quiet local act and
    /// believing it is a message to their DM. <b>It stops short of implying the DM will not find
    /// out</b> — they will, the next time that player joins, and the sentence says so. Anything
    /// stronger would be the privacy claim R-1.7a forbids.
    /// </para>
    /// </remarks>
    /// <param name="sessionCode">The code being forgotten, so the player knows which one.</param>
    public static string BeforeForgetting(string sessionCode) =>
        $"Forget {sessionCode}?\n\n"
        + "This DM will no longer recognise you under this code. The next time you join it you "
        + "arrive as a new player, and the DM approves you the same way they did the first time.\n\n"
        + "Nothing is sent to the DM about this. They find out when you next join, because you "
        + "arrive as someone new.\n\n"
        + "This cannot be undone.";
}
