using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DungeonMasterXIV.Campaigns;

public sealed class CampaignFileArchive : ICampaignArchive
{
    private readonly DirectoryInfo _directory;

    public CampaignFileArchive(DirectoryInfo directory) => _directory = directory;

    public IReadOnlyList<string> CampaignFiles() =>
        NamesMatching($"{CampaignFileName.Prefix}*{CampaignFileName.Suffix}", CampaignFileName.IsCampaignFileName);

    public string? ReadCampaign(string name) =>
        CampaignFileName.IsCampaignFileName(name) ? ReadIfPresent(name) : null;

    public void WriteCampaign(string name, string contents)
    {
        if (!CampaignFileName.IsCampaignFileName(name))
        {
            throw new ArgumentException($"Not a campaign file name: '{name}'.", nameof(name));
        }

        _directory.Create();
        File.WriteAllText(Path.Combine(_directory.FullName, name), contents);
    }

    public string? ReadLegacy() => ReadIfPresent(CampaignFileName.LegacyFileName);

    public IReadOnlyList<string> OtherOwnedFiles()
    {
        var preserved = NamesMatching(
            $"{PreservedCampaignFile.Prefix}*{PreservedCampaignFile.Suffix}",
            PreservedCampaignFile.IsPreservedName);

        return File.Exists(Path.Combine(_directory.FullName, CampaignFileName.LegacyFileName))
            ? preserved.Prepend(CampaignFileName.LegacyFileName).ToArray()
            : preserved;
    }

    public bool Delete(string name)
    {
        if (!IsOurs(name))
        {
            return false;
        }

        var path = Path.Combine(_directory.FullName, name);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private static bool IsOurs(string? name) =>
        CampaignFileName.IsCampaignFileName(name)
        || PreservedCampaignFile.IsPreservedName(name)
        || string.Equals(name, CampaignFileName.LegacyFileName, StringComparison.Ordinal);

    private string? ReadIfPresent(string name)
    {
        var path = Path.Combine(_directory.FullName, name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private string[] NamesMatching(string pattern, Func<string, bool> keep)
    {
        _directory.Refresh();
        if (!_directory.Exists)
        {
            return Array.Empty<string>();
        }

        return _directory
            .EnumerateFiles(pattern)
            .Select(file => file.Name)
            .Where(keep)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
