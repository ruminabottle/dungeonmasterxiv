using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

public interface IRetainedLogArchive
{
    IReadOnlyList<Guid> Campaigns();

    string? Read(Guid campaignId);

    void Write(Guid campaignId, string contents);

    bool Delete(Guid campaignId);
}
