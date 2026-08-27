using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// An in-memory <see cref="ICampaignArchive"/> that records what happened to it, so a test can
/// assert what reached disk and what was left untouched.
/// </summary>
internal sealed class FakeCampaignArchive : ICampaignArchive
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <param name="legacy">Contents of an old single-file store, if this machine has one.</param>
    public FakeCampaignArchive(string? legacy = null)
    {
        if (legacy is not null)
        {
            _files[CampaignFileName.LegacyFileName] = legacy;
        }
    }

    /// <summary>Every file currently present, by name.</summary>
    public IReadOnlyDictionary<string, string> Files => _files;

    /// <summary>Names written, in order, including repeats. Empty means nothing was written.</summary>
    public List<string> Writes { get; } = new();

    /// <summary>Names deleted, in order.</summary>
    public List<string> Deletes { get; } = new();

    /// <summary>
    /// When set, writing this name throws — used to interrupt a migration part-way and check the
    /// old file survives to be retried.
    /// </summary>
    public string? FailWriteForName { get; set; }

    /// <summary>Puts a file in the folder without going through the write path.</summary>
    public void Place(string name, string contents) => _files[name] = contents;

    public IReadOnlyList<string> CampaignFiles() =>
        _files.Keys.Where(CampaignFileName.IsCampaignFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    public string? ReadCampaign(string name) => _files.GetValueOrDefault(name);

    public void WriteCampaign(string name, string contents)
    {
        if (string.Equals(name, FailWriteForName, StringComparison.Ordinal))
        {
            throw new IOException($"Simulated failure writing '{name}'.");
        }

        _files[name] = contents;
        Writes.Add(name);
    }

    public string? ReadLegacy() => _files.GetValueOrDefault(CampaignFileName.LegacyFileName);

    public IReadOnlyList<string> OtherOwnedFiles() =>
        _files.Keys
            .Where(name => PreservedCampaignFile.IsPreservedName(name)
                || string.Equals(name, CampaignFileName.LegacyFileName, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    public bool Delete(string name)
    {
        Deletes.Add(name);
        return _files.Remove(name);
    }
}
