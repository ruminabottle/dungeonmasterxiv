namespace DungeonMasterXIV.Campaigns;

/// <summary>Receives the information and warning messages written while loading campaigns and deleting their files.</summary>
public interface ICampaignStoreLog
{
    void Information(string message);

    void Warning(string message);
}
