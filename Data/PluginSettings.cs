namespace DungeonMasterXIV.Data;

/// <summary>
/// The settings themselves, as a plain serializable type with no Dalamud dependency.
/// <see cref="Configuration"/> is the thin adapter that hands this to Dalamud's config mechanism.
/// The skeleton stores window state and nothing else — the session, campaign and character data
/// described in the brief are not part of it.
/// </summary>
public sealed class PluginSettings
{
    /// <summary>
    /// The schema version written by this build. Bump it whenever the shape of this type changes
    /// in a way that settings already on a user's disk would not survive being read as-is.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Whether the main window was open when the plugin was last unloaded.</summary>
    public bool MainWindowOpen { get; set; }

    /// <summary>Whether the settings window was open when the plugin was last unloaded.</summary>
    public bool SettingsWindowOpen { get; set; }

    /// <summary>
    /// When true, windows reopen on load if they were open on unload. When false, the plugin
    /// always starts with everything closed.
    /// </summary>
    public bool RestoreWindowState { get; set; } = true;

    /// <summary>
    /// Whether a window that was open on unload should be reopened on load.
    /// </summary>
    /// <param name="wasOpen">The remembered open state of that window.</param>
    public bool ShouldOpenOnLoad(bool wasOpen) => RestoreWindowState && wasOpen;
}
