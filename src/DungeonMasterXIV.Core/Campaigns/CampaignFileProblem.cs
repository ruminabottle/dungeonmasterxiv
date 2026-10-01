namespace DungeonMasterXIV.Campaigns;

/// <summary>Why a campaign file is listed: it won't parse, an earlier build left it, or it still holds campaigns.</summary>
public enum CampaignFileProblem
{
    WillNotParse,

    LeftByAnEarlierBuild,

    StillHoldsCampaigns,
}
