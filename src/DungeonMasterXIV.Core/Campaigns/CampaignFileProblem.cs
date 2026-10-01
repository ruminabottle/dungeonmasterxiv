namespace DungeonMasterXIV.Campaigns;

/// <summary>Why a listed file is unusable: it won't parse, is a legacy or preserved file, or still holds campaigns.</summary>
public enum CampaignFileProblem
{
    WillNotParse,

    LeftByAnEarlierBuild,

    StillHoldsCampaigns,
}
