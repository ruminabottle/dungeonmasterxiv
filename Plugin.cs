using System;
using System.IO;
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

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/dmx";
    private const string CampaignsCommandName = "/dmxcampaigns";

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

        var characterName = new LocalCharacterName(objects).Current;

        _hostingCampaign = new HostingCampaign(_campaignStore);
        _configWindow = SettingsWindowFor(characterName);
        var sessionLog = new SessionTransportLog(log);
        _relayTransport = new WebSocketSessionTransport(sessionLog);
        _sessionCoordinator = new SessionCoordinator(
            _relayTransport,
            () => _configurationStore.Configuration.Settings.RelayAddress,
            _configurationStore.Configuration.Settings.InterruptionWindowOrDefault(),
            log: sessionLog,
            capabilities: new SessionCapabilities(
                HostDisplayName: NameWeSendAs(characterName),
                MintParticipant: label => _hostingCampaign.Current is { } campaign
                    ? _campaignStore.AddParticipant(campaign.CampaignId, label.Value)?.ParticipantId
                    : null,
                ResolveRelink: claimed => CampaignRelink.Resolve(_hostingCampaign.Current, claimed)));
        _sessionWindow = new SessionWindow(
            _sessionCoordinator,
            NameWeSendAs(characterName),
            _hostingCampaign,
            () => _configurationStore.Configuration.Settings.Relink, SessionEndChoiceFor(pluginInterface.ConfigDirectory));
        _mainWindow.OpenSession = _sessionWindow.Open;
        _campaignListWindow = CampaignListWindowFor(pluginInterface.ConfigDirectory);
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

    private Func<DisplayName> NameWeSendAs(Func<DisplayName> characterName) =>
        () => CampaignDisplayName.Or(_hostingCampaign.Current, characterName());

    private ConfigWindow SettingsWindowFor(Func<DisplayName> characterName) =>
        new(_configurationStore, characterName, () => _hostingCampaign.Current, _campaignStore.Save);

    private CampaignListWindow CampaignListWindowFor(DirectoryInfo configDirectory)
    {
        var retainedLogs = new RetainedLogStore(
            new RetainedLogFileArchive(Path.Combine(configDirectory.FullName, "logs")));
        return new CampaignListWindow(_campaignStore, new CampaignDeletion(_campaignStore, retainedLogs));
    }

    private KeepOrLose SessionEndChoiceFor(DirectoryInfo configDirectory) =>
        new(
            KeepOrLoseTheSessionLog,
            new SessionExportFileDestination(Path.Combine(configDirectory.FullName, "exports")));

    private SessionLogOffer KeepOrLoseTheSessionLog()
    {
        var now = DateTimeOffset.UtcNow;

        return new SessionLogOffer(
            new RetainedLog(Guid.Empty, now.UtcTicks, StreamLogProjection.From(_sessionCoordinator.Recorded)),
            now.Add(TimeSpan.FromSeconds(60)).UtcTicks);
    }

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

        _unwind.Push("session and relay connection", () =>
        {
            _sessionCoordinator.EndSessionForTeardown(DateTimeOffset.UtcNow);
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

        framework.Update += OnFrameworkUpdate;
        _unwind.Push("framework update handler", () => framework.Update -= OnFrameworkUpdate);
    }

    private void Unwind() => _unwind.UnwindAll(
        (step, exception) => _log.Error(
            exception,
            "Teardown step '{Step}' failed. The remaining steps still ran; the plugin is fully unwound.",
            step));

    private void OnCommand(string command, string arguments) => _commandDispatcher.Execute(arguments);

    private void OnFrameworkUpdate(IFramework framework)
    {
        _sessionCoordinator.Tick(framework.UpdateDelta, DateTimeOffset.UtcNow);
        RememberWhoWeAre();
    }

    private void RememberWhoWeAre()
    {
        var join = _sessionCoordinator.Join;

        if (join.Phase != JoinPhase.Admitted
            || join.ParticipantId is not { } participantId
            || join.Code is not { } code)
        {
            return;
        }

        if (_configurationStore.Configuration.Settings.Relink.Remember(code, participantId))
        {
            _configurationStore.Save();
        }
    }

    private void OnCampaignsCommand(string command, string arguments) => _campaignListWindow.Open();
}
