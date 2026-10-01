using Dalamud.Configuration;

namespace DungeonMasterXIV.Data;

/// <summary>The configuration Dalamud saves for the plugin: a schema version and the plugin settings.</summary>
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = PluginSettings.CurrentSchemaVersion;

    public PluginSettings Settings { get; set; } = new();
}
