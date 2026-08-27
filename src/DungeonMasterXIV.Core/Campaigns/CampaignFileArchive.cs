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
/// <see cref="DeletePreserved"/> could not be tested where it lives. A guard about paths belongs at
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
    private const string FileName = "campaigns.json";

    private readonly DirectoryInfo _directory;

    /// <param name="directory">Where campaign files live — the plugin's own config directory.</param>
    public CampaignFileArchive(DirectoryInfo directory) => _directory = directory;

    private string FilePath => Path.Combine(_directory.FullName, FileName);

    /// <inheritdoc />
    public string? Read() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;

    /// <inheritdoc />
    public void Write(string contents)
    {
        _directory.Create();
        File.WriteAllText(FilePath, contents);
    }

    /// <inheritdoc />
    public string PreserveUnreadable()
    {
        var keptName = PreservedCampaignFile.NameFor(DateTimeOffset.UtcNow);
        File.Move(FilePath, Path.Combine(_directory.FullName, keptName));
        return keptName;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> PreservedFiles()
    {
        _directory.Refresh();
        if (!_directory.Exists)
        {
            return Array.Empty<string>();
        }

        return _directory
            .EnumerateFiles($"{PreservedCampaignFile.Prefix}*{PreservedCampaignFile.Suffix}")
            .Select(file => file.Name)
            .Where(PreservedCampaignFile.IsPreservedName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public bool DeletePreserved(string name)
    {
        // The only check on this name, and it is here rather than in the caller because this is the
        // layer that turns it into a path. Without it, "../../dalamudUI.ini" combines to a real
        // file outside the config directory and File.Exists below would happily agree.
        if (!PreservedCampaignFile.IsPreservedName(name))
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
}
