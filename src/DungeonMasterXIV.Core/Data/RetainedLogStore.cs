using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Data;

/// <summary>Keeps formatted session logs per campaign in an archive, retaining one only when hosting.</summary>
public sealed class RetainedLogStore(IRetainedLogArchive archive)
{
    private readonly IRetainedLogArchive _archive =
        archive ?? throw new ArgumentNullException(nameof(archive));

    public bool Retain(RetainedLog log, bool isHosting)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (!LogRetention.KeepsItsLog(isHosting))
        {
            return false;
        }

        _archive.Write(log.CampaignId, RetainedLogFormat.Write(log));
        return true;
    }

    public bool Has(Guid campaignId) => _archive.Read(campaignId) is not null;

    public string? Read(Guid campaignId) => _archive.Read(campaignId);

    public IReadOnlyList<Guid> Retained() => _archive.Campaigns().ToList();

    public bool DeleteFor(Guid campaignId) => _archive.Delete(campaignId);
}
