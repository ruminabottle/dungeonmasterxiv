using System;
using System.IO;
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
        var keptName = $"campaigns.unreadable-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}.json";
        File.Move(FilePath, Path.Combine(_directory.FullName, keptName));
        return keptName;
    }
}
