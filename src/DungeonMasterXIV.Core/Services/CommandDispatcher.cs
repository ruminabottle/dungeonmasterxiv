using System;

namespace DungeonMasterXIV.Services;

/// <summary>
/// Maps the argument of the <c>/dmx</c> command onto the window it addresses. This lives in a
/// service rather than in the command handler so that <c>Plugin.cs</c> stays wiring only, and so
/// the command surface can be tested without an ImGui harness.
/// </summary>
public sealed class CommandDispatcher
{
    private const string SettingsArgument = "settings";

    private readonly Action _toggleMainWindow;
    private readonly Action _openSettingsWindow;

    /// <param name="toggleMainWindow">Invoked for <c>/dmx</c> and for any unrecognised argument.</param>
    /// <param name="openSettingsWindow">Invoked for <c>/dmx settings</c>.</param>
    public CommandDispatcher(Action toggleMainWindow, Action openSettingsWindow)
    {
        _toggleMainWindow = toggleMainWindow;
        _openSettingsWindow = openSettingsWindow;
    }

    /// <summary>
    /// Runs the action the argument names. An argument we do not recognise falls through to the
    /// main window rather than failing silently, because <c>/dmx</c> with no argument is by far
    /// the common case and a typo should still get the user somewhere.
    /// </summary>
    /// <param name="arguments">Everything the user typed after <c>/dmx</c>.</param>
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
