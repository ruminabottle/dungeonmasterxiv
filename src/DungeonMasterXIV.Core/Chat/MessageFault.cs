namespace DungeonMasterXIV.Chat;

/// <summary>
/// Why a composed message was refused. <see cref="None"/> when it was not.
/// </summary>
/// <remarks>
/// <b>A named fault rather than a bare false, because A-2.35 is about the sender KNOWING.</b> The
/// criterion puts silent truncation and silent dropping in the same failing category, and both of
/// those are what a boolean refusal turns into at the first call site that does not bother to say
/// anything. Naming the fault here is what lets the compose surface tell the person what happened.
/// </remarks>
public enum MessageFault
{
    /// <summary>No fault; the message was accepted.</summary>
    None = 0,

    /// <summary>There was nothing to send.</summary>
    Empty,

    /// <summary>Longer than <see cref="MessageLimits.MaxLength"/>, as a reader counts characters.</summary>
    TooLong,

    /// <summary>
    /// Within <see cref="MessageLimits.MaxLength"/> characters but over
    /// <see cref="MessageLimits.MaxUtf8Bytes"/> encoded.
    /// </summary>
    /// <remarks>
    /// <b>This is the bound <see cref="TooLong"/> cannot express, and it is the one a hostile peer
    /// aims at.</b> A grapheme cluster may carry arbitrarily many combining marks, so a string of N
    /// characters has no finite byte ceiling — <see cref="DungeonMasterXIV.Net.DisplayName"/> records
    /// the same finding for names. A character bound alone therefore bounds what a person perceives
    /// and not what the wire carries.
    /// </remarks>
    TooLarge,

    /// <summary>
    /// There was nowhere to send it — this client is not in a session, or was never admitted.
    /// </summary>
    /// <remarks>
    /// <b>Distinct from <see cref="Empty"/> because the two are different things to be told.</b>
    /// Empty means the person typed nothing; this means they typed something and there is no session
    /// to carry it. Folding the second into the first reports a fault the sender can see is false,
    /// which is its own way of failing A-2.35's <i>the fault is named</i>.
    /// </remarks>
    NotInASession,
}
