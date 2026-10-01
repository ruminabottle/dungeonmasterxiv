using System;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Resolves a claimed participant identifier against a campaign's participants into a relink claim.</summary>
public static class CampaignRelink
{
    public static RelinkClaim Resolve(Campaign? campaign, string? claimedParticipantId)
    {
        if (campaign is null || !Guid.TryParseExact(claimedParticipantId, "D", out var claimed))
        {
            return RelinkClaim.None;
        }

        var participant = campaign.Participants.FirstOrDefault(known => known.ParticipantId == claimed);

        return participant is null ? RelinkClaim.None : new RelinkClaim(true, participant.Label);
    }
}
