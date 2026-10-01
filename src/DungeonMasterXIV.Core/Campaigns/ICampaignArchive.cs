using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Storage for campaign files: list, read, write and delete them, and read the single-file store.</summary>
public interface ICampaignArchive
{
    IReadOnlyList<string> CampaignFiles();

    string? ReadCampaign(string name);

    void WriteCampaign(string name, string contents);

    bool Delete(string name);

    string? ReadLegacy();

    IReadOnlyList<string> OtherOwnedFiles();
}
