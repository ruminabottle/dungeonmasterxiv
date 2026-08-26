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
    /// Schema version of the settings in this object. <see cref="ConfigurationStore.Save"/> stamps
    /// it with <see cref="PluginSettings.CurrentSchemaVersion"/> before every write, so it always
    /// describes the shape that was actually written. The version that arrived from disk is a
    /// different question and is answered by <see cref="ConfigurationStore.LoadedVersion"/>.
    /// </summary>
    public int Version { get; set; } = PluginSettings.CurrentSchemaVersion;

    /// <summary>The settings this build actually reads and writes.</summary>
    public PluginSettings Settings { get; set; } = new();
}
