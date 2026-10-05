using System;

namespace DungeonMasterXIV.Data;

/// <summary>The tabs the plugin's panel switches between.</summary>
public enum PanelTab
{
    Chat = 0,

    Session = 1,

    Settings = 2,
}

/// <summary>The plugin's saved settings: panel state, relay address, interruption window, relink memory, alias.</summary>
public sealed class PluginSettings
{
    public const int CurrentSchemaVersion = 1;

    public bool MainWindowOpen { get; set; }

    public bool RestoreWindowState { get; set; } = true;

    public bool PanelCollapsed { get; set; }

    public PanelTab SelectedTab { get; set; } = PanelTab.Session;

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

    public bool RecordPanelCollapsed(bool collapsed)
    {
        if (PanelCollapsed == collapsed)
        {
            return false;
        }

        PanelCollapsed = collapsed;
        return true;
    }

    public bool RecordSelectedTab(PanelTab tab)
    {
        if (SelectedTab == tab)
        {
            return false;
        }

        SelectedTab = tab;
        return true;
    }
}
