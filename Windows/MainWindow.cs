using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Data;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// The plugin's main window. In the skeleton it states what the plugin is and that no session is
/// running; the session, roll and initiative views arrive with their own PRDs.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly ConfigurationStore _configurationStore;

    /// <param name="configurationStore">Used to remember whether this window was left open.</param>
    public MainWindow(ConfigurationStore configurationStore)
        : base("Dungeon Master XIV##dmx-main")
    {
        _configurationStore = configurationStore;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(360, 160),
            MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue),
        };

        IsOpen = configurationStore.Configuration.Settings.ShouldOpenOnLoad(
            configurationStore.Configuration.Settings.MainWindowOpen);
    }

    /// <inheritdoc />
    public override void Draw()
    {
        ImGui.TextWrapped(
            "Dungeon Master XIV tracks dice rolls, initiative and combatant state for tabletop RP " +
            "campaigns run in game.");
        ImGui.Separator();
        ImGui.TextUnformatted("No session is running.");
    }

    /// <inheritdoc />
    public override void OnOpen() => Remember(true);

    /// <inheritdoc />
    public override void OnClose() => Remember(false);

    private void Remember(bool isOpen)
    {
        if (_configurationStore.Configuration.Settings.RecordMainWindowOpen(isOpen))
        {
            _configurationStore.Save();
        }
    }
}
