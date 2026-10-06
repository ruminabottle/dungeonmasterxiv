namespace DungeonMasterXIV.Net;

/// <summary>Who a message or roll is for: everyone, the DM side, blind to the DM side, or one player and the DM side.</summary>
public enum AudienceKind
{
    Public = 0,

    DmSide = 1,

    Blind = 2,

    Player = 3,
}

/// <summary>The audience a sender asked for; <see cref="To"/> is the player's peer code when the kind is Player.</summary>
public sealed record MessageAudience(AudienceKind Kind, string? To = null)
{
    public static MessageAudience Public { get; } = new(AudienceKind.Public);

    public static MessageAudience DmSide { get; } = new(AudienceKind.DmSide);

    public static MessageAudience Blind { get; } = new(AudienceKind.Blind);

    public static MessageAudience ToPlayer(string peerCode) => new(AudienceKind.Player, peerCode);

    /// <summary>What goes on the wire: nothing for Public, and DM side for text sent while Blind is chosen.</summary>
    public MessageAudience? ForSending(bool isRoll) => Kind switch
    {
        AudienceKind.Public => null,
        AudienceKind.Blind when !isRoll => DmSide,
        _ => this,
    };
}
