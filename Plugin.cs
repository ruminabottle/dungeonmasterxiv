using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Services;
using DungeonMasterXIV.Transport;
using DungeonMasterXIV.Windows;

namespace DungeonMasterXIV;

/// <summary>
/// Plugin entry point. Construction, registration and teardown only — every decision this plugin
/// makes lives in a service or a window.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/dmx";

    /// <summary>
    /// How to undo each registration that actually completed, newest first. Dalamud does not call
    /// <see cref="Dispose"/> on a plugin whose constructor threw, so registration records its own
    /// undo as it goes and the constructor unwinds this itself on the way out. Popping in LIFO
    /// order is what guarantees the reverse of construction — including unsubscribing Draw before
    /// any window leaves the window system, so no frame can land on a half-emptied one.
    /// </summary>
    private readonly Stack<Action> _unwind = new();

    private readonly IPluginLog _log;
    private readonly ConfigurationStore _configurationStore;
    private readonly WindowSystem _windowSystem;
    private readonly MainWindow _mainWindow;
    private readonly ConfigWindow _configWindow;
    private readonly SessionWindow _sessionWindow;
    private readonly RelayTransport _relayTransport;
    private readonly SessionCoordinator _sessionCoordinator;
    private readonly CommandDispatcher _commandDispatcher;

    /// <summary>Constructs the plugin from the Dalamud services injected by the host.</summary>
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        _log = log;

        _configurationStore = new ConfigurationStore(pluginInterface, log);
        _windowSystem = new WindowSystem("DungeonMasterXIV");
        _mainWindow = new MainWindow(_configurationStore);
        _configWindow = new ConfigWindow(_configurationStore);
        _relayTransport = new RelayTransport(log);
        _sessionCoordinator = new SessionCoordinator(
            _relayTransport,
            () => _configurationStore.Configuration.Settings.RelayAddress);
        _sessionWindow = new SessionWindow(_sessionCoordinator);
        _mainWindow.OpenSession = _sessionWindow.Open;
        _commandDispatcher = new CommandDispatcher(_mainWindow.Toggle, _configWindow.Open);

        try
        {
            Register(pluginInterface, commandManager);
        }
        catch
        {
            Unwind();
            throw;
        }

        _log.Information("Dungeon Master XIV loaded.");
    }

    /// <summary>Unwinds construction in reverse order.</summary>
    public void Dispose()
    {
        Unwind();
        _log.Information("Dungeon Master XIV unloaded.");
    }

    private void Register(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        _windowSystem.AddWindow(_mainWindow);
        _unwind.Push(() => _windowSystem.RemoveWindow(_mainWindow));

        _windowSystem.AddWindow(_configWindow);
        _unwind.Push(() => _windowSystem.RemoveWindow(_configWindow));

        _windowSystem.AddWindow(_sessionWindow);
        _unwind.Push(() => _windowSystem.RemoveWindow(_sessionWindow));

        // R-1.1: unloading the plugin ends the session, and ending it drops the relay connection.
        // Registered as an unwind step so it runs on a constructor throw as well as on Dispose.
        _unwind.Push(() =>
        {
            _sessionCoordinator.StopHosting();
            _relayTransport.Dispose();
        });

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Dungeon Master XIV window. \"/dmx settings\" opens settings.",
        });
        _unwind.Push(() => commandManager.RemoveHandler(CommandName));

        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _unwind.Push(() => pluginInterface.UiBuilder.Draw -= _windowSystem.Draw);

        pluginInterface.UiBuilder.OpenMainUi += _mainWindow.Toggle;
        _unwind.Push(() => pluginInterface.UiBuilder.OpenMainUi -= _mainWindow.Toggle);

        pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;
        _unwind.Push(() => pluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle);
    }

    private void Unwind()
    {
        while (_unwind.Count > 0)
        {
            _unwind.Pop()();
        }
    }

    private void OnCommand(string command, string arguments) => _commandDispatcher.Execute(arguments);
}
