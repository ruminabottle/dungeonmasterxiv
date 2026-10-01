namespace DungeonMasterXIV.Net;

/// <summary>Decides whether a reader may see a message given its target, sender, and the reader's role.</summary>
public static class MessageAudience
{
    public static bool Includes(
        MessageTarget target,
        PeerCode sender,
        PeerCode reader,
        SessionRole readerRole)
    {
        if (!sender.IsPresent || !reader.IsPresent)
        {
            return false;
        }

        return target switch
        {
            MessageTarget.Everyone => true,
            MessageTarget.DungeonMasterOnly =>
                readerRole is SessionRole.DungeonMaster || reader.Equals(sender),

            _ => false,
        };
    }
}
