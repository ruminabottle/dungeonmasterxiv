namespace DungeonMasterXIV.Net;

/// <summary>The kind of event a session stream entry records.</summary>
public enum StreamEventKind
{
    Message,

    Roll,

    Joined,

    Left,

    Dropped,

    Reconnected,

    Gap,
}
