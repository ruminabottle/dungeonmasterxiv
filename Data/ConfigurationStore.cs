using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Reads and writes <see cref="Configuration"/> through Dalamud's plugin config mechanism, so
/// nothing above this layer needs to know where settings live on disk.
/// </summary>
public sealed class ConfigurationStore
{
    private readonly IDalamudPluginInterface _pluginInterface;

    /// <summary>
    /// Loads the stored settings. A first run and a config that could not be read both end up on
    /// defaults, so they are logged differently — losing every setting should leave a signal for
    /// whoever ends up supporting it.
    /// </summary>
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
    }

    /// <summary>The live settings. Mutate through this, then call <see cref="Save"/>.</summary>
    public Configuration Configuration { get; }

    /// <summary>
    /// The schema version that came off disk, or <c>null</c> when there was nothing readable to
    /// load. This is where a migration belongs: it is the only point that knows which shape
    /// arrived, and <see cref="Configuration"/>'s own version is overwritten on the next save.
    /// </summary>
    public int? LoadedVersion { get; }

    /// <summary>
    /// Writes the current settings to disk, stamped with the schema version they are written in.
    /// </summary>
    public void Save()
    {
        Configuration.Version = PluginSettings.CurrentSchemaVersion;
        _pluginInterface.SavePluginConfig(Configuration);
    }
}
