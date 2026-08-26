using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Services;
using DungeonMasterXIV.Windows;

namespace DungeonMasterXIV;

/// <summary>
/// Plugin entry point. Construction, registration and teardown only — every decision this plugin
/// makes lives in a service or a window.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/dmx";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICommandManager _commandManager;
    private readonly IPluginLog _log;

    private readonly ConfigurationStore _configurationStore;
    private readonly WindowSystem _windowSystem;
    private readonly MainWindow _mainWindow;
    private readonly ConfigWindow _configWindow;
    private readonly CommandDispatcher _commandDispatcher;

    /// <summary>Constructs the plugin from the Dalamud services injected by the host.</summary>
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;
        _log = log;

        _configurationStore = new ConfigurationStore(pluginInterface);

        _windowSystem = new WindowSystem("DungeonMasterXIV");
        _mainWindow = new MainWindow(_configurationStore);
        _configWindow = new ConfigWindow(_configurationStore);
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_configWindow);

        _commandDispatcher = new CommandDispatcher(_mainWindow.Toggle, _configWindow.Open);

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the DungeonMasterXIV window. \"/dmx settings\" opens settings.",
        });

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += _mainWindow.Toggle;
        _pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;

        _log.Information("DungeonMasterXIV loaded.");
    }

    /// <summary>Unwinds construction in reverse order.</summary>
    public void Dispose()
    {
        _pluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle;
        _pluginInterface.UiBuilder.OpenMainUi -= _mainWindow.Toggle;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

        _commandManager.RemoveHandler(CommandName);

        _windowSystem.RemoveWindow(_configWindow);
        _windowSystem.RemoveWindow(_mainWindow);

        _log.Information("DungeonMasterXIV unloaded.");
    }

    private void OnCommand(string command, string arguments) => _commandDispatcher.Execute(arguments);
}
