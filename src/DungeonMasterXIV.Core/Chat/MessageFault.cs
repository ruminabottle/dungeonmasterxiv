namespace DungeonMasterXIV.Chat;

/// <summary>Why a chat message was refused, if it was: empty, too long, too many bytes, or not in a session.</summary>
public enum MessageFault
{
    None = 0,

    Empty,

    TooLong,

    TooLarge,

    NotInASession,
}
