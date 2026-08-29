using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Campaigns;
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
    private const string CampaignsCommandName = "/dmxcampaigns";

    /// <summary>
    /// How to undo each registration that actually completed, newest first. Dalamud does not call
    /// <see cref="Dispose"/> on a plugin whose constructor threw, so registration records its own
    /// undo as it goes and the constructor unwinds this itself on the way out. Unwinding in LIFO
    /// order is what guarantees the reverse of construction — including unsubscribing Draw before
    /// any window leaves the window system, so no frame can land on a half-emptied one. Each step is
    /// isolated from the others, so one that throws cannot abandon the ones still queued behind it.
    /// </summary>
    private readonly TeardownSequence _unwind = new();

    private readonly IPluginLog _log;
    private readonly ConfigurationStore _configurationStore;
    private readonly CampaignStore _campaignStore;
    private readonly WindowSystem _windowSystem;
    private readonly MainWindow _mainWindow;
    private readonly ConfigWindow _configWindow;
    private readonly SessionWindow _sessionWindow;
    private readonly WebSocketSessionTransport _relayTransport;
    private readonly SessionCoordinator _sessionCoordinator;
    private readonly HostingCampaign _hostingCampaign;
    private readonly CampaignListWindow _campaignListWindow;
    private readonly CommandDispatcher _commandDispatcher;

    /// <summary>Constructs the plugin from the Dalamud services injected by the host.</summary>
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IFramework framework,
        IObjectTable objects)
    {
        _log = log;

        _configurationStore = new ConfigurationStore(pluginInterface, log);
        _campaignStore = new CampaignStore(
            new CampaignFileArchive(pluginInterface.ConfigDirectory),
            new CampaignStoreLog(log));
        _windowSystem = new WindowSystem("DungeonMasterXIV");
        _mainWindow = new MainWindow(_configurationStore);

        // R-1.3e. Hoisted above the windows because BOTH of them need it: the settings window shows
        // the name that will be sent, and the join sends it. One supplier, so the two cannot
        // disagree about what this player is called.
        var characterName = new LocalCharacterName(objects).Current;

        _configWindow = new ConfigWindow(_configurationStore, characterName);
        // ONE adapter, TWO consumers. The coordinator needs it so that a roster entry dropped on
        // the way in is observable to a developer rather than silent (BUG-70) -- the codec has said
        // so since #120, but nothing production-side was listening until this line existed.
        var sessionLog = new SessionTransportLog(log);
        _relayTransport = new WebSocketSessionTransport(sessionLog);
        // A-1.23/A-1.27: the ONE settable value, read from settings and validated on the way out
        // rather than trusted from disk. This is the only production construction of the
        // coordinator, so this is the only place the windows can get their length -- which is what
        // makes "no literal in the grace path" structural rather than something a test hopes for.
        // The connection that did not exist until DMXENG-48: the store and the coordinator were both
        // built here and never joined, so no session had a campaign and AddParticipant had no
        // production caller at all.
        //
        // MOVED ABOVE THE COORDINATOR RATHER THAN LEFT BELOW IT AND COMMENTED. The minter below
        // closes over this field, and a closure that is merely not-invoked-yet is the ordering
        // dependency DMXENG-45 exists because of -- one nothing detects, which a reorder turns into
        // a null nobody refuses. Constructing it first removes the dependency instead of
        // documenting it; it needs only the store, which exists well above here.
        _hostingCampaign = new HostingCampaign(_campaignStore);
        _sessionCoordinator = new SessionCoordinator(
            _relayTransport,
            () => _configurationStore.Configuration.Settings.RelayAddress,
            _configurationStore.Configuration.Settings.InterruptionWindowOrDefault(),
            log: sessionLog,
            // R-1.5c half 1, and the line that gives AddParticipant its FIRST production caller.
            // Until now nothing minted a participant in the shipped build at all, so the joiner had
            // nothing to be told and relink was a protocol capability away rather than a call away.
            //
            // Null when no campaign is current, which is every JOINING client -- an ordinary state,
            // not a failure. A HOST that reaches here with no campaign is a different matter and
            // AdmissionControl warns on it by peer code, so the quiet version of this cannot ship.
            mintParticipant: label => _hostingCampaign.Current is { } campaign
                ? _campaignStore.AddParticipant(campaign.CampaignId, label.Value)?.ParticipantId
                : null);
        // The alias if the player set a usable one, otherwise the character name (R-1.3e). The rule
        // lives in PluginSettings so it is testable without Dalamud, and the settings window calls
        // the same method to show what will be sent -- one expression, so the preview cannot drift
        // from the thing it previews.
        _sessionWindow = new SessionWindow(
            _sessionCoordinator,
            () => _configurationStore.Configuration.Settings.DisplayNameOr(characterName()),
            _hostingCampaign);
        _mainWindow.OpenSession = _sessionWindow.Open;
        _campaignListWindow = new CampaignListWindow(_campaignStore);
        _commandDispatcher = new CommandDispatcher(_mainWindow.Toggle, _configWindow.Open);

        try
        {
            Register(pluginInterface, commandManager, framework);
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

    private void Register(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IFramework framework)
    {
        _windowSystem.AddWindow(_mainWindow);
        _unwind.Push("main window", () => _windowSystem.RemoveWindow(_mainWindow));

        _windowSystem.AddWindow(_configWindow);
        _unwind.Push("settings window", () => _windowSystem.RemoveWindow(_configWindow));

        _windowSystem.AddWindow(_sessionWindow);
        _unwind.Push("session window", () => _windowSystem.RemoveWindow(_sessionWindow));

        // R-1.1: unloading the plugin ends the session, and ending it drops the relay connection.
        // Registered as an unwind step so it runs on a constructor throw as well as on Dispose.
        _unwind.Push("session and relay connection", () =>
        {
            _sessionCoordinator.StopHosting(DateTimeOffset.UtcNow);
            _sessionCoordinator.Detach();
            _relayTransport.Dispose();
        });

        _windowSystem.AddWindow(_campaignListWindow);
        _unwind.Push("campaign list window", () => _windowSystem.RemoveWindow(_campaignListWindow));

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Dungeon Master XIV window. \"/dmx settings\" opens settings.",
        });
        _unwind.Push("/dmx command", () => commandManager.RemoveHandler(CommandName));

        commandManager.AddHandler(CampaignsCommandName, new CommandInfo(OnCampaignsCommand)
        {
            HelpMessage = "List the campaigns stored on this machine.",
        });
        _unwind.Push("/dmxcampaigns command", () => commandManager.RemoveHandler(CampaignsCommandName));

        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _unwind.Push("draw handler", () => pluginInterface.UiBuilder.Draw -= _windowSystem.Draw);

        pluginInterface.UiBuilder.OpenMainUi += _mainWindow.Toggle;
        _unwind.Push("main UI handler", () => pluginInterface.UiBuilder.OpenMainUi -= _mainWindow.Toggle);

        pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;
        _unwind.Push("config UI handler", () => pluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle);

        // The timeouts A-1.5b depends on are only reachable if something calls them every frame.
        framework.Update += OnFrameworkUpdate;
        _unwind.Push("framework update handler", () => framework.Update -= OnFrameworkUpdate);
    }

    // A step that throws must not abandon the steps still queued behind it: the transport teardown
    // sits above three RemoveWindow calls, and skipping those leaves windows registered against a
    // disposed plugin. That surfaces on the NEXT enable as a duplicate window rather than here, so
    // the failure and the symptom are separated by a user action (A-0.6, BUG-8).
    private void Unwind() => _unwind.UnwindAll(
        (step, exception) => _log.Error(
            exception,
            "Teardown step '{Step}' failed. The remaining steps still ran; the plugin is fully unwound.",
            step));

    private void OnCommand(string command, string arguments) => _commandDispatcher.Execute(arguments);

    private void OnFrameworkUpdate(IFramework framework) => _sessionCoordinator.Tick(framework.UpdateDelta, DateTimeOffset.UtcNow);

    private void OnCampaignsCommand(string command, string arguments) => _campaignListWindow.Open();
}
