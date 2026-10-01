using Dalamud.Configuration;

namespace DungeonMasterXIV.Data;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = PluginSettings.CurrentSchemaVersion;

    public PluginSettings Settings { get; set; } = new();
}
