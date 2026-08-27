using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Keeps the campaign document in a file in the plugin's config directory.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately mechanism only: paths, bytes and renames. Every decision about what to do with
/// what comes back — first run, unreadable, preserve rather than replace — belongs to
/// <see cref="CampaignStore"/>, which is testable because it holds no Dalamud type.
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

    /// <param name="pluginInterface">Supplies the plugin's own config directory.</param>
    public CampaignFileArchive(IDalamudPluginInterface pluginInterface) =>
        _directory = pluginInterface.ConfigDirectory;

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
        // Checked here as well as in the store: this is the layer holding the path, so it does not
        // rely on a caller having validated first.
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
