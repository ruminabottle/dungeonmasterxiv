using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// The settings window. The skeleton has one setting — whether windows reopen where they were
/// left — because window state is the only thing the skeleton persists.
/// </summary>
public sealed class ConfigWindow : Window
{
    private readonly ConfigurationStore _configurationStore;

    // Built once: Draw runs every frame and the schema version cannot change while we are loaded.
    private readonly string _schemaVersionLabel;

    // R-1.7a, verbatim. Bold markers from the requirement are dropped because ImGui text has no
    // bold; nothing else is altered. If this needs to change, R-1.7a changes first.
    private static readonly string[] WhatThisPluginKnows =
    {
        "During a session, this plugin knows who is in the room. The game gives it character names, "
        + "and nothing can change that. What it does with them is the part we control: names are "
        + "never written to a log, never included in an export, and never linked between one "
        + "campaign and another.",
        "Your session is encrypted end to end. The relay passes messages between you and cannot "
        + "read them. It can still see that a connection exists, roughly when and how much, and the "
        + "network address it came from - encryption hides what you say, not that you are talking.",
        "Campaign history stays on the DM's machine. There is no account, no server storing your "
        + "sessions, and nothing to delete anywhere but here.",
    };

    private const string PlainTransportWarning =
        "This relay address is not encrypted in transit. Your session payloads are still encrypted "
        + "end to end, but who you connect to is visible to anyone on the network path.";

    private const string InvalidRelayWarning =
        "This is not a usable relay address. It must start with wss:// or ws://.";

    /// <param name="configurationStore">The settings this window reads and writes.</param>
    public ConfigWindow(ConfigurationStore configurationStore)
        : base("Dungeon Master XIV settings###dmx-settings")
    {
        _configurationStore = configurationStore;
        _schemaVersionLabel = $"Settings schema version {configurationStore.Configuration.Version}";

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(360, 140),
            MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue),
        };

        IsOpen = configurationStore.Configuration.Settings.ShouldOpenOnLoad(
            configurationStore.Configuration.Settings.SettingsWindowOpen);
    }

    /// <summary>Opens this window, for the <c>/dmx settings</c> command.</summary>
    public void Open() => IsOpen = true;

    /// <inheritdoc />
    public override void Draw()
    {
        var settings = _configurationStore.Configuration.Settings;

        var restore = settings.RestoreWindowState;
        if (ImGui.Checkbox("Reopen windows where I left them", ref restore))
        {
            settings.RestoreWindowState = restore;
            _configurationStore.Save();
        }

        ImGui.Separator();
        DrawRelaySetting(settings);

        ImGui.Separator();
        DrawWhatThisPluginKnows();

        ImGui.Separator();
        ImGui.TextDisabled(_schemaVersionLabel);
    }

    // R-1.8: the relay is swappable and the setting is discoverable rather than buried.
    private void DrawRelaySetting(PluginSettings settings)
    {
        ImGui.TextUnformatted("Relay");
        var address = settings.RelayAddress;
        if (ImGui.InputText("Relay address", ref address, 256))
        {
            settings.RelayAddress = address;
            _configurationStore.Save();
        }

        if (RelayEndpoint.TryParse(settings.RelayAddress, out var endpoint))
        {
            if (!RelayEndpoint.IsEncryptedTransport(endpoint!))
            {
                ImGui.TextWrapped(PlainTransportWarning);
            }
        }
        else
        {
            ImGui.TextWrapped(InvalidRelayWarning);
        }
    }

    // R-1.7a. These strings are literal and a PR may not substitute its own wording; they are
    // reproduced here exactly as the requirement states them.
    private static void DrawWhatThisPluginKnows()
    {
        ImGui.TextUnformatted("What this plugin knows");
        ImGui.Spacing();

        foreach (var paragraph in WhatThisPluginKnows)
        {
            ImGui.TextWrapped(paragraph);
            ImGui.Spacing();
        }
    }

    /// <inheritdoc />
    public override void OnOpen() => Remember(true);

    /// <inheritdoc />
    public override void OnClose() => Remember(false);

    private void Remember(bool isOpen)
    {
        if (_configurationStore.Configuration.Settings.RecordSettingsWindowOpen(isOpen))
        {
            _configurationStore.Save();
        }
    }
}
