using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Settles what a sender asked for into a private entry's audience and entitled seats, or refuses it.</summary>
public static class AudienceRules
{
    public const string ForTheDm = "rolled for the DM";

    public const string ByTheDm = "rolled";

    /// <summary>False when the request must be dropped; a null privacy means public. A null sender is the host.</summary>
    public static bool TryResolve(
        MessageAudience? requested,
        bool isRoll,
        AdmittedPeer? sender,
        SessionAudience audience,
        out EntryPrivacy? privacy)
    {
        privacy = null;
        if (requested is null || requested.Kind == AudienceKind.Public)
        {
            return true;
        }

        var fromHost = sender is null;
        var seats = audience.Recipients
            .Where(peer => peer.Role == SessionRole.Assistant)
            .Select(SeatKey.For)
            .ToList();
        if (sender is not null)
        {
            seats.Add(SeatKey.For(sender));
        }

        switch (requested.Kind)
        {
            case AudienceKind.DmSide:
            case AudienceKind.Blind:
                var kind = requested.Kind == AudienceKind.Blind && isRoll && !fromHost
                    ? AudienceKind.Blind
                    : AudienceKind.DmSide;
                privacy = new EntryPrivacy(
                    new MessageAudience(kind), seats.Distinct().ToList(), fromHost ? ByTheDm : ForTheDm);
                return true;

            case AudienceKind.Player when fromHost
                && requested.To is { } to
                && PeerCode.TryParse(to, out var code)
                && audience.Find(code) is { Role: SessionRole.Player } target
                && audience.SupportsAudiences(code):
                seats.Add(SeatKey.For(target));
                privacy = new EntryPrivacy(
                    MessageAudience.ToPlayer(to), seats.Distinct().ToList(), $"rolled for {target.DisplayName.Value}");
                return true;

            default:
                return false;
        }
    }
}
