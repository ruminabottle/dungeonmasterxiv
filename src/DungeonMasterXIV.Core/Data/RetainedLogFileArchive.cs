using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Retained logs as files on the DM's machine, beside campaign data (R-2.12).
/// </summary>
/// <remarks>
/// <para>
/// <b>"On their machine, where campaign data lives" is the requirement's wording and this reads it
/// as BESIDE rather than INSIDE.</b> A log is not campaign data — a campaign persists a roster,
/// which is metadata, while a log is what people said and did. Its own directory keeps that true on
/// disk as well as in the types, and moving it into the campaign store would be a product decision
/// rather than a storage one.
/// </para>
/// <para>
/// <b>One file per campaign, named by its id</b>, so deletion is a single file operation keyed on
/// exactly what the delete control already knows.
/// </para>
/// <para>
/// <b>A missing directory is not an error.</b> A DM who has never hosted has no logs, and asking
/// for them must answer "none" rather than throwing — the same shape as
/// <c>CampaignFileArchive</c>'s handling of a first run.
/// </para>
/// </remarks>
public sealed class RetainedLogFileArchive(string directory) : IRetainedLogArchive
{
    private const string Extension = ".log.txt";

    private readonly string _directory =
        directory ?? throw new ArgumentNullException(nameof(directory));

    /// <inheritdoc/>
    public IReadOnlyList<Guid> Campaigns()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        return Directory.GetFiles(_directory, $"*{Extension}")
            .Select(path => Path.GetFileName(path)[..^Extension.Length])
            .Select(name => Guid.TryParse(name, out var id) ? id : (Guid?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();
    }

    /// <inheritdoc/>
    public string? Read(Guid campaignId)
    {
        var path = PathFor(campaignId);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <inheritdoc/>
    public void Write(Guid campaignId, string contents)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(PathFor(campaignId), contents);
    }

    /// <inheritdoc/>
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

    /// <summary>
    /// The file a campaign's log lives in. <b>Built from the Guid's own formatting</b>, never from
    /// caller-supplied text, so no path separator or traversal sequence can reach it.
    /// </summary>
    private string PathFor(Guid campaignId) =>
        Path.Combine(_directory, $"{campaignId}{Extension}");
}
