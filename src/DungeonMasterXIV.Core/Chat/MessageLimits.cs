namespace DungeonMasterXIV.Chat;

/// <summary>The most characters and UTF-8 bytes a chat message may hold: by default 2000, at 8 bytes each.</summary>
public sealed record MessageLimits
{
    public static MessageLimits Default { get; } = new();

    public int MaxLength { get; init; } = 2000;

    public int MaxUtf8Bytes => MaxLength * BytesPerCharacter;

    public int BytesPerCharacter { get; init; } = 8;
}
