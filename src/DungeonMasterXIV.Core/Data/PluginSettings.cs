namespace DungeonMasterXIV.Data;

/// <summary>
/// The settings themselves, as a plain serializable type with no Dalamud dependency.
/// <c>Configuration</c>, over in the plugin project, is the thin adapter that hands this to
/// Dalamud's config mechanism — it cannot be named in a cref from here, because this project
/// deliberately cannot see it. The skeleton stores window state and nothing else; the session,
/// campaign and character data described in the brief are not part of it.
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
    /// Whether loading should write the settings straight back out. True only when nothing
    /// readable came off disk, so a first run leaves a schema version behind without the user
    /// having to open anything, and a config already at the current version is not rewritten
    /// identically on every load. A version we do not recognise is left for a migration to deal
    /// with rather than stamped down to this one.
    /// </summary>
    /// <param name="versionOnDisk">The schema version that was loaded, or <c>null</c> if none was.</param>
    public static bool RequiresWriteOnLoad(int? versionOnDisk) => versionOnDisk is null;

    /// <summary>
    /// The relay this client dials. R-1.8 requires this to be swappable and discoverable — the
    /// default is a default, not a dependency. Validated by
    /// <c>RelayEndpoint.TryParse</c> before use rather than trusted from disk.
    /// </summary>
    public string RelayAddress { get; set; } = Net.RelayEndpoint.Default;

    /// <summary>
    /// Whether a window that was open on unload should be reopened on load.
    /// </summary>
    /// <param name="wasOpen">The remembered open state of that window.</param>
    public bool ShouldOpenOnLoad(bool wasOpen) => RestoreWindowState && wasOpen;

    /// <summary>
    /// Records the main window's open state, reporting whether that changed anything. Callers
    /// save only when it returns true, so reopening the plugin does not rewrite an identical file.
    /// </summary>
    /// <param name="isOpen">The window's new open state.</param>
    public bool RecordMainWindowOpen(bool isOpen)
    {
        if (MainWindowOpen == isOpen)
        {
            return false;
        }

        MainWindowOpen = isOpen;
        return true;
    }

    /// <summary>
    /// Records the settings window's open state, reporting whether that changed anything.
    /// </summary>
    /// <param name="isOpen">The window's new open state.</param>
    public bool RecordSettingsWindowOpen(bool isOpen)
    {
        if (SettingsWindowOpen == isOpen)
        {
            return false;
        }

        SettingsWindowOpen = isOpen;
        return true;
    }
}
