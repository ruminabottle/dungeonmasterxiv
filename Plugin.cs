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

        _hostingCampaign = new HostingCampaign(_campaignStore);
        _configWindow = SettingsWindowFor(characterName);
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
            capabilities: new SessionCapabilities(
                HostDisplayName: NameWeSendAs(characterName),
                MintParticipant: label => _hostingCampaign.Current is { } campaign
                    ? _campaignStore.AddParticipant(campaign.CampaignId, label.Value)?.ParticipantId
                    : null,
                // T-37, and the line that gives CampaignRelink.Resolve its FIRST production caller.
                // Until now every one of its eight call sites was in tests: a claim arrived on the
                // wire and every relink branch took the not-a-relink path, so the host could not
                // approve a relink no matter what a client sent.
                //
                // Current campaign, read at the MOMENT OF THE REQUEST rather than captured -- the DM
                // may start or resume a campaign while the session is live, and a claim must be
                // resolved against the roster that is actually loaded.
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

    /// <summary>What this client sends as its name, host side and join side alike (R-1.3e).</summary>
    /// <remarks>
    /// <para>
    /// <b>ONE expression, called twice, rather than two that mean to agree.</b>
    /// <c>CampaignDisplayName.Or</c> is the single rule for "the alias if the player set a usable
    /// one, otherwise the character name" — it lives in <c>Core/Campaigns</c> so it is testable
    /// without Dalamud, and the settings window calls the same method to show what will be sent.
    /// <b>It answers for ONE campaign</b> (R-2.17, D-8): the name is scoped, so the rule has to be
    /// told which campaign it is answering for rather than reading a global alias. <b>So the
    /// name the DM publishes in its own roster entry (A-1.13b), the name a joiner sends, and the
    /// preview the session window draws cannot drift apart</b>, which the comment here used to
    /// claim while a second copy of the expression sat four lines below it.
    /// </para>
    /// <para>
    /// <b>A method rather than lines in the constructor, and the reason is a measurement.</b>
    /// <c>Plugin</c>'s constructor is 91 lines against a 60 capacity — a pre-existing breach nobody
    /// on this branch created (BUG-103). Putting new code inline would have taken it to 97.
    /// <b>Declining to enlarge a breach is not the same as repairing one</b>: the other 91 lines
    /// are not this chunk's to touch, but where its own lines go is its to choose.
    /// </para>
    /// </remarks>
    private Func<DisplayName> NameWeSendAs(Func<DisplayName> characterName) =>
        () => CampaignDisplayName.Or(_hostingCampaign.Current, characterName());

    /// <summary>The settings window, wired to the campaign the display name is scoped to.</summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="_hostingCampaign"/> is constructed BEFORE this runs, not closed over and hoped
    /// for.</b> The coordinator below records why: <i>a closure that is merely not-invoked-yet is
    /// the ordering dependency DMXENG-45 exists because of — one nothing detects, which a reorder
    /// turns into a null nobody refuses.</i> Same argument here, so the same remedy.
    /// </para>
    /// <para>
    /// <b>A method rather than lines in the constructor, and the reason is a measurement.</b>
    /// <c>Plugin</c>'s constructor is a grandfathered breach at 88 lines against a 60 capacity
    /// (BUG-103). Inline these lines and it reaches 95 — <b>the size gate refused exactly that,</b>
    /// naming the margin moving from -28 to -35. Grandfathered breaches may stay where they are;
    /// they may not grow. The same reasoning already put <see cref="NameWeSendAs"/> here.
    /// </para>
    /// </remarks>
    /// <param name="characterName">What the game says this player is called.</param>
    private ConfigWindow SettingsWindowFor(Func<DisplayName> characterName) =>
        new(_configurationStore, characterName, () => _hostingCampaign.Current, _campaignStore.Save);

    /// <summary>
    /// The campaign list window and the retained-log side its delete control must reach (R-2.12).
    /// Logs sit BESIDE campaign data — <see cref="CampaignDeletion"/> says why that is a ruling.
    /// </summary>
    /// <param name="configDirectory">Where Dalamud keeps this plugin's data; logs go beside it.</param>
    private CampaignListWindow CampaignListWindowFor(DirectoryInfo configDirectory)
    {
        var retainedLogs = new RetainedLogStore(
            new RetainedLogFileArchive(Path.Combine(configDirectory.FullName, "logs")));
        return new CampaignListWindow(_campaignStore, new CampaignDeletion(_campaignStore, retainedLogs));
    }

    /// <summary>
    /// The session-end choice: how to open the offer, and where an accepted export goes (A-2.23a).
    /// </summary>
    /// <remarks>
    /// <b>A method rather than five lines in the constructor, and the size gate is why</b> — that
    /// constructor is a grandfathered breach at margin -28, and a grandfathered breach may stay
    /// where it is but may not GROW. Inlining this worsened it to -35 and the gate refused the
    /// tree, which is the gate working.
    /// </remarks>
    /// <param name="configDirectory">Where Dalamud keeps this plugin's data; exports go beside it.</param>
    private KeepOrLose SessionEndChoiceFor(DirectoryInfo configDirectory) =>
        new(
            KeepOrLoseTheSessionLog,
            // A separate directory from "logs": that one holds the DM's RETAINED logs, keyed by
            // campaign and permitted to carry a peer code (A-1.11a-note). These may not, and filing
            // them apart keeps the two obligations from meeting in one folder.
            new SessionExportFileDestination(Path.Combine(configDirectory.FullName, "exports")));

    /// <summary>
    /// Opens R-2.12's keep-or-lose choice over what THIS client recorded (A-2.23).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The entries are copied here, not referenced</b>, so the log the offer holds outlives the
    /// departure that follows it — SQ-115's requirement that <i>the log survives until the choice
    /// resolves</i>, arranged as a hold on one object rather than a hold on the teardown.
    /// </para>
    /// <para>
    /// <b>THE SIXTY SECONDS ARE ENGINEERING'S AND NO REQUIREMENT STATES THEM.</b> R-1.3c requires
    /// only that the wait is bounded and its bound shown; SQ-115 put the arrangement here in terms.
    /// The figure matches the closing window the player is already watching under R-1.3g, so the
    /// two countdowns on one screen cannot disagree — <b>a second number would be the drift R-1.3c
    /// names.</b> It is deliberately not read from settings: a user-settable window would make the
    /// bound something a person could set to a value that fails the requirement.
    /// </para>
    /// <para>
    /// <b>The campaign id is empty because a JOINER HAS NONE</b>, and this log is never stored or
    /// keyed by one — a player's log dies unless kept, so it never reaches the archive that
    /// deletion works through. The DM's retained log, which does carry a campaign, is a different
    /// path entirely (<c>SessionLogRetention</c>).
    /// </para>
    /// </remarks>
    private SessionLogOffer KeepOrLoseTheSessionLog()
    {
        var now = DateTimeOffset.UtcNow;

        return new SessionLogOffer(
            new RetainedLog(Guid.Empty, now.UtcTicks, StreamLogProjection.From(_sessionCoordinator.Recorded)),
            now.Add(TimeSpan.FromSeconds(60)).UtcTicks);
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
        // R-1.1: unloading the plugin ends the session, and ending it drops the relay connection.
        // BUG-154: the three calls this replaced were the HOST's half only, so a joiner quitting the
        // game deliberately looked exactly like one that vanished and had its seat held five minutes.
        // The ordered sequence lives in EndSessionForTeardown because the ORDER is the fix and a
        // lambda here cannot be tested -- the test project reaches Core and not this project.
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

    // R-1.5b's storing half. HERE RATHER THAN IN A WINDOW, because persisting is not drawing and a
    // Draw method runs only while a window is open -- a player who closed the join window mid-
    // admission would never have been told to remember anything.
    //
    // SAVES ONLY WHEN THE MEMORY ACTUALLY CHANGED. This runs at frame rate, so an unconditional
    // Save() here would write the config file sixty times a second: Remember returns whether
    // anything moved and that is the whole guard.
    private void OnFrameworkUpdate(IFramework framework)
    {
        _sessionCoordinator.Tick(framework.UpdateDelta, DateTimeOffset.UtcNow);
        RememberWhoWeAre();
    }

    // The host told us which participant we are (R-1.5c) and this is what makes it survive the
    // process. Guarded on Admitted so nothing is written from an attempt that was refused, lapsed or
    // is still waiting -- R-1.3b, and JoinAttempt already refuses to hold an id outside Admitted.
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
