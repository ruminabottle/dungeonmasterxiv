using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace DungeonMasterXIV.Data;

public sealed class ConfigurationStore
{
    private readonly IDalamudPluginInterface _pluginInterface;

    public ConfigurationStore(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _pluginInterface = pluginInterface;

        switch (pluginInterface.GetPluginConfig())
        {
            case Configuration stored:
                Configuration = stored;
                LoadedVersion = stored.Version;
                break;

            case null:
                Configuration = new Configuration();
                log.Information("No stored settings found. Starting from defaults; this is a first run.");
                break;

            default:
                Configuration = new Configuration();
                log.Warning("Stored settings could not be read and have been replaced with defaults. Previous settings are lost.");
                break;
        }

        if (PluginSettings.RequiresWriteOnLoad(LoadedVersion))
        {
            Save();
        }
    }

    public Configuration Configuration { get; }

    public int? LoadedVersion { get; }

    public void Save()
    {
        Configuration.Version = PluginSettings.CurrentSchemaVersion;
        _pluginInterface.SavePluginConfig(Configuration);
    }
}
