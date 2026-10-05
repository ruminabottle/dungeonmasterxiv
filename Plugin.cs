using System;
using System.IO;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Services;
using DungeonMasterXIV.Transport;
using DungeonMasterXIV.Windows;
using DungeonMasterXIV.Windows.Ui;

namespace DungeonMasterXIV;

/// <summary>The Dalamud plugin entry point: builds services and windows, registers commands, and unwinds them.</summary>
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/dmx";

    private readonly TeardownSequence _unwind = new();

    private readonly IPluginLog _log;
    private readonly ConfigurationStore _configurationStore;
    private readonly CampaignStore _campaignStore;
    private readonly WindowSystem _windowSystem;
    private readonly UiFonts _fonts;
    private readonly RailWindow _railWindow;
    private readonly ConfigWindow _configWindow;
    private readonly SessionWindow _sessionWindow;
    private readonly WebSocketSessionTransport _relayTransport;
    private readonly SessionCoordinator _sessionCoordinator;
    private readonly HostingCampaign _hostingCampaign;
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
        _fonts = new UiFonts(
            pluginInterface.UiBuilder,
            Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "Data", "Fonts"),
            log);

        var characterName = new LocalCharacterName(objects).Current;

        _hostingCampaign = new HostingCampaign(_campaignStore);
        _configWindow = SettingsWindowFor(characterName, pluginInterface.ConfigDirectory, log);
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
                ResolveRelink: claimed => CampaignRelink.Resolve(_hostingCampaign.Current, claimed),
                LetReturningPlayersIn: () => _hostingCampaign.LetsReturningPlayersIn));
        _sessionWindow = new SessionWindow(
            _sessionCoordinator,
            _fonts,
            log,
            NameWeSendAs(characterName),
            _hostingCampaign,
            () => _configurationStore.Configuration.Settings.Relink, SessionEndChoiceFor(pluginInterface.ConfigDirectory));
        _railWindow = new RailWindow(
            _configurationStore,
            _fonts,
            log,
            [new RailEntry(_sessionWindow, FontAwesomeIcon.Comments, "Session")],
            new RailEntry(_configWindow, FontAwesomeIcon.Cog, "Settings"),
            _sessionWindow);
        _commandDispatcher = new CommandDispatcher(_railWindow.Toggle, _configWindow.Open);

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

    private ConfigWindow SettingsWindowFor(Func<DisplayName> characterName, DirectoryInfo configDirectory, IPluginLog log)
    {
        var retainedLogs = new RetainedLogStore(
            new RetainedLogFileArchive(Path.Combine(configDirectory.FullName, "logs")));

        return new ConfigWindow(
            _configurationStore,
            characterName,
            () => _hostingCampaign.Current,
            _campaignStore.Save,
            new CampaignStorageView(_campaignStore, new CampaignDeletion(_campaignStore, retainedLogs)),
            _fonts,
            log);
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
        _unwind.Push("fonts", _fonts.Dispose);

        _windowSystem.AddWindow(_railWindow);
        _unwind.Push("rail window", () => _windowSystem.RemoveWindow(_railWindow));

        _windowSystem.AddWindow(_configWindow);
        _unwind.Push("settings window", () => _windowSystem.RemoveWindow(_configWindow));

        _windowSystem.AddWindow(_sessionWindow);
        _unwind.Push("session window", () => _windowSystem.RemoveWindow(_sessionWindow));

        _unwind.Push("session and relay connection", () =>
        {
            _sessionCoordinator.EndSessionForTeardown(DateTimeOffset.UtcNow);
            _relayTransport.Dispose();
        });

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Dungeon Master XIV buttons. \"/dmx settings\" opens settings.",
        });
        _unwind.Push("/dmx command", () => commandManager.RemoveHandler(CommandName));

        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _unwind.Push("draw handler", () => pluginInterface.UiBuilder.Draw -= _windowSystem.Draw);

        pluginInterface.UiBuilder.OpenMainUi += _railWindow.Toggle;
        _unwind.Push("main UI handler", () => pluginInterface.UiBuilder.OpenMainUi -= _railWindow.Toggle);

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
}
