using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

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
