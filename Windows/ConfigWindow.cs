using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Data;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// The settings window. The skeleton has one setting — whether windows reopen where they were
/// left — because window state is the only thing the skeleton persists.
/// </summary>
public sealed class ConfigWindow : Window
{
    private readonly ConfigurationStore _configurationStore;

    // Built once: Draw runs every frame and the schema version cannot change while we are loaded.
    private readonly string _schemaVersionLabel;

    /// <param name="configurationStore">The settings this window reads and writes.</param>
    public ConfigWindow(ConfigurationStore configurationStore)
        : base("Dungeon Master XIV settings##dmx-settings")
    {
        _configurationStore = configurationStore;
        _schemaVersionLabel = $"Settings schema version {configurationStore.Configuration.Version}";

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(360, 140),
            MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue),
        };

        IsOpen = configurationStore.Configuration.Settings.ShouldOpenOnLoad(
            configurationStore.Configuration.Settings.SettingsWindowOpen);
    }

    /// <summary>Opens this window, for the <c>/dmx settings</c> command.</summary>
    public void Open() => IsOpen = true;

    /// <inheritdoc />
    public override void Draw()
    {
        var settings = _configurationStore.Configuration.Settings;

        var restore = settings.RestoreWindowState;
        if (ImGui.Checkbox("Reopen windows where I left them", ref restore))
        {
            settings.RestoreWindowState = restore;
            _configurationStore.Save();
        }

        ImGui.Separator();
        ImGui.TextDisabled(_schemaVersionLabel);
    }

    /// <inheritdoc />
    public override void OnOpen() => Remember(true);

    /// <inheritdoc />
    public override void OnClose() => Remember(false);

    private void Remember(bool isOpen)
    {
        if (_configurationStore.Configuration.Settings.RecordSettingsWindowOpen(isOpen))
        {
            _configurationStore.Save();
        }
    }
}
