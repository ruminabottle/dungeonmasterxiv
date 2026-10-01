using System;
using System.IO;

namespace DungeonMasterXIV.Data;

/// <summary>Deletes a campaign's retained session log, stored as one .log.txt file per campaign in a directory.</summary>
public sealed class RetainedLogFileArchive(string directory) : IRetainedLogArchive
{
    private const string Extension = ".log.txt";

    private readonly string _directory =
        directory ?? throw new ArgumentNullException(nameof(directory));

    public bool Delete(Guid campaignId)
    {
        var path = PathFor(campaignId);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private string PathFor(Guid campaignId) =>
        Path.Combine(_directory, $"{campaignId}{Extension}");
}
