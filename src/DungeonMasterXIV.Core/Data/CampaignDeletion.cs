using System;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Data;

/// <summary>Deletes a campaign together with its retained session log.</summary>
public sealed class CampaignDeletion(CampaignStore campaigns, RetainedLogStore logs)
{
    private readonly CampaignStore _campaigns =
        campaigns ?? throw new ArgumentNullException(nameof(campaigns));

    private readonly RetainedLogStore _logs =
        logs ?? throw new ArgumentNullException(nameof(logs));

    public bool Delete(Guid campaignId) => _campaigns.Delete(campaignId) | _logs.DeleteFor(campaignId);
}
