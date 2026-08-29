using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Finding a campaign in a list that is already in hand.</summary>
/// <remarks>
/// Separate from <c>CampaignStore.Find</c>, which searches the STORE. A caller holding a list it
/// just read should not go back to the store to resolve an id out of it.
/// </remarks>
public static class CampaignLookup
{
    /// <summary>The campaign with that id, or null.</summary>
    /// <param name="campaigns">The list to search.</param>
    /// <param name="campaignId">The id to find.</param>
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
