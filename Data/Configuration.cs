using Dalamud.Configuration;

namespace DungeonMasterXIV.Data;

/// <summary>
/// What Dalamud's plugin config mechanism stores. Deliberately a thin adapter: it satisfies
/// <see cref="IPluginConfiguration"/> and carries <see cref="PluginSettings"/>, so the settings
/// themselves stay free of any Dalamud reference and can be tested on their own.
/// </summary>
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>
    /// Schema version of the stored settings — the value that was read from disk, or
    /// <see cref="PluginSettings.CurrentSchemaVersion"/> on a first run. Kept as written rather
    /// than restamped on save, so a future migration can tell which shape it is dealing with.
    /// </summary>
    public int Version { get; set; } = PluginSettings.CurrentSchemaVersion;

    /// <summary>The settings this build actually reads and writes.</summary>
    public PluginSettings Settings { get; set; } = new();
}
