using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>The audience chosen above the message box, shared so the roster can pick a player to message.</summary>
internal sealed class AudienceChoice
{
    private int _lastSeenStarts;

    public AudienceKind Kind { get; private set; } = AudienceKind.Public;

    public string? To { get; private set; }

    public MessageAudience Current =>
        Kind == AudienceKind.Player && To is { } to ? MessageAudience.ToPlayer(to) : new MessageAudience(Kind);

    public void Choose(AudienceKind kind)
    {
        Kind = kind;
        To = null;
    }

    public void ChoosePlayer(string peerCode)
    {
        Kind = AudienceKind.Player;
        To = peerCode;
    }

    public void Reset() => Choose(AudienceKind.Public);

    /// <summary>Resets to Public when the session start count has changed since the last call, then remembers it.</summary>
    public void Sync(int sessionStarts)
    {
        if (sessionStarts != _lastSeenStarts)
        {
            Reset();
        }

        _lastSeenStarts = sessionStarts;
    }
}
