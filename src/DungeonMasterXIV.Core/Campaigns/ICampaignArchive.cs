using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

public interface ICampaignArchive
{
    IReadOnlyList<string> CampaignFiles();

    string? ReadCampaign(string name);

    void WriteCampaign(string name, string contents);

    bool Delete(string name);

    string? ReadLegacy();

    IReadOnlyList<string> OtherOwnedFiles();
}
