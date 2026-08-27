using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Keeps the campaign document in a file in the plugin's config directory.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately mechanism only: paths, bytes and renames. Every decision about what to do with
/// what comes back — first run, unreadable, preserve rather than replace — belongs to
/// <see cref="CampaignStore"/>.
/// </para>
/// <para>
/// <b>This takes a directory rather than Dalamud's plugin interface, and that is the point.</b> It
/// used to take <c>IDalamudPluginInterface</c> purely to read <c>ConfigDirectory</c>, which made
/// the whole type unreachable from the test assembly — so the guard on
/// <see cref="Delete"/> could not be tested where it lives. A guard about paths belongs at
/// the layer holding the path, and it is only worth having if it can be exercised.
/// </para>
/// <para>
/// A separate file rather than Dalamud's plugin config: the config mechanism is one blob for
/// settings, and campaign history is state a DM would miss. Keeping them apart means a settings
/// problem cannot cost a campaign, and vice versa.
/// </para>
/// <para>
/// I/O failures are not caught. An unreadable-because-malformed document is a case the store
/// handles; an unreadable-because-the-disk-said-no document is not something this plugin can
/// paper over, and the "no swallowed exceptions" rule makes it loud at load rather than silent.
/// </para>
/// </remarks>
public sealed class CampaignFileArchive : ICampaignArchive
{
    private readonly DirectoryInfo _directory;

    /// <param name="directory">Where campaign files live — the plugin's own config directory.</param>
    public CampaignFileArchive(DirectoryInfo directory) => _directory = directory;

    /// <inheritdoc />
    public IReadOnlyList<string> CampaignFiles() =>
        NamesMatching($"{CampaignFileName.Prefix}*{CampaignFileName.Suffix}", CampaignFileName.IsCampaignFileName);

    /// <inheritdoc />
    public string? ReadCampaign(string name) =>
        CampaignFileName.IsCampaignFileName(name) ? ReadIfPresent(name) : null;

    /// <inheritdoc />
    public void WriteCampaign(string name, string contents)
    {
        if (!CampaignFileName.IsCampaignFileName(name))
        {
            throw new ArgumentException($"Not a campaign file name: '{name}'.", nameof(name));
        }

        _directory.Create();
        File.WriteAllText(Path.Combine(_directory.FullName, name), contents);
    }

    /// <inheritdoc />
    public string? ReadLegacy() => ReadIfPresent(CampaignFileName.LegacyFileName);

    /// <inheritdoc />
    public IReadOnlyList<string> OtherOwnedFiles()
    {
        var preserved = NamesMatching(
            $"{PreservedCampaignFile.Prefix}*{PreservedCampaignFile.Suffix}",
            PreservedCampaignFile.IsPreservedName);

        return File.Exists(Path.Combine(_directory.FullName, CampaignFileName.LegacyFileName))
            ? preserved.Prepend(CampaignFileName.LegacyFileName).ToArray()
            : preserved;
    }

    /// <inheritdoc />
    public bool Delete(string name)
    {
        // The only check on this name, and it is here because this is the layer that turns it into
        // a path. Without it "../../dalamudUI.ini" combines to a real file outside the config
        // directory and File.Exists below would happily agree.
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
