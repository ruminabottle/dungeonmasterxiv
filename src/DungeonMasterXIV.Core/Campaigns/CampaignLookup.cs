using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Finds a campaign in a list by its identifier.</summary>
public static class CampaignLookup
{
    public static Campaign? FirstOrDefaultById(this IReadOnlyList<Campaign> campaigns, System.Guid campaignId)
    {
        foreach (var campaign in campaigns)
        {
            if (campaign.CampaignId == campaignId)
            {
                return campaign;
            }
        }

        return null;
    }
}
