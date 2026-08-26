using Dalamud.Plugin;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Reads and writes <see cref="Configuration"/> through Dalamud's plugin config mechanism, so
/// nothing above this layer needs to know where settings live on disk.
/// </summary>
public sealed class ConfigurationStore
{
    private readonly IDalamudPluginInterface _pluginInterface;

    /// <summary>
    /// Loads the stored configuration, falling back to defaults when this is a first run.
    /// </summary>
    public ConfigurationStore(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    }

    /// <summary>The live settings. Mutate through this, then call <see cref="Save"/>.</summary>
    public Configuration Configuration { get; }

    /// <summary>Writes the current settings to disk.</summary>
    public void Save() => _pluginInterface.SavePluginConfig(Configuration);
}
