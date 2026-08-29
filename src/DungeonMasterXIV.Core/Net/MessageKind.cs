namespace DungeonMasterXIV.Net;

/// <summary>What register a message is in: in-character, out-of-character, or an emote (R-2.5).</summary>
/// <remarks>
/// <b>THE THREE MUST BE DISTINGUISHABLE WITHOUT RELYING ON COLOUR ALONE</b>, because colour is the
/// one channel a reader may not have. So the kind is not a styling hint — it produces a TEXTUAL
/// affix (<see cref="MessageLine"/>), and a build that encoded it only as a colour would fail R-2.5
/// while passing any test that merely asserted the kind was set.
/// </remarks>
public enum MessageKind
{
    /// <summary>The default: the character speaking.</summary>
    InCharacter,

    /// <summary>The player speaking as themselves — <c>/ooc</c>.</summary>
    OutOfCharacter,

    /// <summary>An action rather than speech — <c>/me</c>.</summary>
    Emote,
}
