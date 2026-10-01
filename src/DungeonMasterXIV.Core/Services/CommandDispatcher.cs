using System;

namespace DungeonMasterXIV.Services;

/// <summary>Acts on the /dmx command's arguments: settings opens settings, anything else toggles the main window.</summary>
public sealed class CommandDispatcher
{
    private const string SettingsArgument = "settings";

    private readonly Action _toggleMainWindow;
    private readonly Action _openSettingsWindow;

    public CommandDispatcher(Action toggleMainWindow, Action openSettingsWindow)
    {
        _toggleMainWindow = toggleMainWindow;
        _openSettingsWindow = openSettingsWindow;
    }

    public void Execute(string arguments)
    {
        if (arguments.Trim().Equals(SettingsArgument, StringComparison.OrdinalIgnoreCase))
        {
            _openSettingsWindow();
            return;
        }

        _toggleMainWindow();
    }
}
