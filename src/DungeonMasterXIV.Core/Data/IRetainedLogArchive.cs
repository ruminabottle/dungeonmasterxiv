using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>Storage for retained session logs, one per campaign: list, read, write and delete them.</summary>
public interface IRetainedLogArchive
{
    IReadOnlyList<Guid> Campaigns();

    string? Read(Guid campaignId);

    void Write(Guid campaignId, string contents);

    bool Delete(Guid campaignId);
}
