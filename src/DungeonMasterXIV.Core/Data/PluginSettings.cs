using System;

namespace DungeonMasterXIV.Data;

/// <summary>The plugin's saved settings: window state, relay address, interruption window, relink memory, name.</summary>
public sealed class PluginSettings
{
    public const int CurrentSchemaVersion = 1;

    public bool MainWindowOpen { get; set; }

    public bool SettingsWindowOpen { get; set; }

    public bool RestoreWindowState { get; set; } = true;

    public static bool RequiresWriteOnLoad(int? versionOnDisk) => versionOnDisk is null;

    public string RelayAddress { get; set; } = Net.RelayEndpoint.Default;

    public TimeSpan InterruptionWindow { get; set; } = Net.GraceWindow.Default;

    public RelinkMemory Relink { get; set; } = new();

    public string DisplayNameAlias { get; set; } = string.Empty;

    public TimeSpan InterruptionWindowOrDefault() =>
        Net.TransportContract.IsKeepAliveSafeFor(InterruptionWindow)
            ? InterruptionWindow
            : Net.GraceWindow.Default;

    public bool RecordInterruptionWindow(TimeSpan window)
    {
        if (!Net.TransportContract.IsKeepAliveSafeFor(window) || InterruptionWindow == window)
        {
            return false;
        }

        InterruptionWindow = window;
        return true;
    }

    public bool ShouldOpenOnLoad(bool wasOpen) => RestoreWindowState && wasOpen;

    public bool RecordMainWindowOpen(bool isOpen)
    {
        if (MainWindowOpen == isOpen)
        {
            return false;
        }

        MainWindowOpen = isOpen;
        return true;
    }

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
