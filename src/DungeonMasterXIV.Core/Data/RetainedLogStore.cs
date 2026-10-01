using System;

namespace DungeonMasterXIV.Data;

/// <summary>Deletes a campaign's retained session log from an archive.</summary>
public sealed class RetainedLogStore(IRetainedLogArchive archive)
{
    private readonly IRetainedLogArchive _archive =
        archive ?? throw new ArgumentNullException(nameof(archive));

    public bool DeleteFor(Guid campaignId) => _archive.Delete(campaignId);
}
