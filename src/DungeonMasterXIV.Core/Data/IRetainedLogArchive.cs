using System;

namespace DungeonMasterXIV.Data;

/// <summary>Storage for retained session logs, one per campaign, that can delete a campaign's log.</summary>
public interface IRetainedLogArchive
{
    bool Delete(Guid campaignId);
}
