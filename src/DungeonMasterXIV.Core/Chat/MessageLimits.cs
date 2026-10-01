namespace DungeonMasterXIV.Chat;

public sealed record MessageLimits
{
    public static MessageLimits Default { get; } = new();

    public int MaxLength { get; init; } = 2000;

    public int MaxUtf8Bytes => MaxLength * BytesPerCharacter;

    public int BytesPerCharacter { get; init; } = 8;
}
