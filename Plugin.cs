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
    private readonly PanelWindow _panel;
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
        _panel = new PanelWindow(_configurationStore, _sessionCoordinator, _fonts, log);
        var joinFlow = new JoinFlowView(
            _sessionCoordinator,
            _fonts,
            NameWeSendAs(characterName),
            () => _configurationStore.Configuration.Settings.Relink,
            SessionEndChoiceFor(pluginInterface.ConfigDirectory));
        _panel.Attach(
            new ChatTab(_sessionCoordinator, _fonts, NameWeSendAs(characterName), joinFlow, () => _panel.Select(PanelTab.Session)),
            new SessionTab(_sessionCoordinator, _fonts, _hostingCampaign, joinFlow),
            SettingsTabFor(characterName, pluginInterface.ConfigDirectory));
        _commandDispatcher = new CommandDispatcher(_panel.Toggle, OpenSettingsTab);

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

    private SettingsTab SettingsTabFor(Func<DisplayName> characterName, DirectoryInfo configDirectory)
    {
        var retainedLogs = new RetainedLogStore(
            new RetainedLogFileArchive(Path.Combine(configDirectory.FullName, "logs")));

        return new SettingsTab(
            _configurationStore,
            characterName,
            () => _hostingCampaign.Current,
            _campaignStore.Save,
            new CampaignStorageView(_campaignStore, new CampaignDeletion(_campaignStore, retainedLogs)),
            _fonts);
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

        _windowSystem.AddWindow(_panel);
        _unwind.Push("panel window", () => _windowSystem.RemoveWindow(_panel));

        _unwind.Push("session and relay connection", () =>
        {
            _sessionCoordinator.EndSessionForTeardown(DateTimeOffset.UtcNow);
            _relayTransport.Dispose();
        });

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Dungeon Master XIV panel. \"/dmx settings\" opens its Settings tab.",
        });
        _unwind.Push("/dmx command", () => commandManager.RemoveHandler(CommandName));

        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _unwind.Push("draw handler", () => pluginInterface.UiBuilder.Draw -= _windowSystem.Draw);

        pluginInterface.UiBuilder.OpenMainUi += _panel.Toggle;
        _unwind.Push("main UI handler", () => pluginInterface.UiBuilder.OpenMainUi -= _panel.Toggle);

        pluginInterface.UiBuilder.OpenConfigUi += OpenSettingsTab;
        _unwind.Push("config UI handler", () => pluginInterface.UiBuilder.OpenConfigUi -= OpenSettingsTab);

        framework.Update += OnFrameworkUpdate;
        _unwind.Push("framework update handler", () => framework.Update -= OnFrameworkUpdate);
    }

    private void Unwind() => _unwind.UnwindAll(
        (step, exception) => _log.Error(
            exception,
            "Teardown step '{Step}' failed. The remaining steps still ran; the plugin is fully unwound.",
            step));

    private void OnCommand(string command, string arguments) => _commandDispatcher.Execute(arguments);

    private void OpenSettingsTab() => _panel.Show(PanelTab.Settings);

    private void OnFrameworkUpdate(IFramework framework)
    {
        _sessionCoordinator.Tick(framework.UpdateDelta, DateTimeOffset.UtcNow);
        _hostingCampaign.Follow(_sessionCoordinator.InAHostedSession);
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
