using System;
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
    private readonly Func<DisplayName> _characterName;

    // Built once: Draw runs every frame and the schema version cannot change while we are loaded.
    private readonly string _schemaVersionLabel;
    private readonly RelinkMemoryView _relinkMemory;

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

    // Not R-1.7a copy -- R-1.7a covers the session window, the admission prompt and the settings
    // section's "What this plugin knows" text, and supplies no wording for these. Written here under
    // the same constraint: no phrasing from its forbidden list, and no claim that a name proves
    // anything.
    private static readonly string UnusableAliasWarning =
        $"This name cannot be used, so your character name will be sent instead. Names are limited "
        + $"to {DisplayName.MaxLength} characters and cannot contain line breaks or invisible "
        + "formatting characters - they are shown next to the code you compare, and a name that can "
        + "redraw that line is a way to hide it.";

    // D-8: a name may be shown and may never be acted on. Said in the place a user chooses one,
    // because that is where somebody would otherwise assume it identifies them.
    private const string NameIsNotIdentity =
        "This name is not checked by anything. Anyone can send any name, so it tells your DM who "
        + "you say you are and nothing more - the code you read to each other is the part that "
        + "proves anything.";

    private const string InvalidRelayWarning =
        "This is not a usable relay address. It must start with wss:// - or ws:// for a relay "
        + "running on this machine.";

    /// <param name="configurationStore">The settings this window reads and writes.</param>
    /// <param name="characterName">
    /// What the game says this player is called, read at draw time rather than captured — a
    /// character name is not stable for the life of the plugin.
    /// </param>
    public ConfigWindow(ConfigurationStore configurationStore, Func<DisplayName> characterName)
        : base("Dungeon Master XIV settings###dmx-settings")
    {
        _configurationStore = configurationStore;
        _characterName = characterName;

        // Both suppliers read through the STORE rather than capturing the settings object, so a
        // configuration reloaded from disk underneath this window is still the one the player sees
        // and deletes from.
        _relinkMemory = new RelinkMemoryView(
            () => _configurationStore.Configuration.Settings.Relink,
            _configurationStore.Save);
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
        DrawDisplayNameSetting(settings);

        ImGui.Separator();
        DrawRelaySetting(settings);

        ImGui.Separator();
        DrawWhatThisPluginKnows();

        // R-1.5b, IMMEDIATELY AFTER "what this plugin knows" AND NOT IN A WINDOW OF ITS OWN. A-1.9b
        // gives the player a right to SEE what is stored about them; a reader already here to answer
        // that question should not have to discover a second place where the rest of the answer is.
        ImGui.Separator();
        ImGui.TextUnformatted("What this plugin remembers about you");
        _relinkMemory.Draw();

        ImGui.Separator();
        ImGui.TextDisabled(_schemaVersionLabel);
    }

    // R-1.3e: the name defaults to the character name and may be changed to an alias.
    //
    // The effective name is SHOWN, not only editable. R-1.3e's Tier 0 is "see and change the name
    // they will send" -- a box you type into without being told the result delivers the changing
    // half and not the seeing half, and the default case is exactly the one where the box is empty
    // and the answer is not obvious.
    private void DrawDisplayNameSetting(PluginSettings settings)
    {
        ImGui.TextUnformatted("Display name");

        var characterName = _characterName();

        // PRE-FILLED with the character name, which is a citation and not a nicety: R-1.3e's Tier 0
        // is "see and change the name they will send, before it is sent, PRE-FILLED with their
        // character name". An empty box fails that — the user would have to already know what would
        // be sent in order to see it.
        //
        // Room to type MORE than the limit, deliberately. A box capped at exactly the limit stops
        // accepting keystrokes with no explanation, and the user is left looking at a name that is
        // not the one they meant. Over-typing is allowed and then told about.
        //
        // The size is MaxUtf8Bytes because IMGUI COUNTS BYTES AND THE LIMIT COUNTS CHARACTERS. This
        // used to be (MaxLength * 2) + 1 = 65, which was room to over-type only in ASCII: a
        // 32-character Devanagari name is 192 bytes and would have been truncated at the boundary
        // this box exists to let the user cross deliberately.
        var typed = settings.NameToEdit(characterName);
        if (ImGui.InputText("Name others see", ref typed, DisplayName.MaxUtf8Bytes))
        {
            if (settings.RecordChosenName(typed, characterName))
            {
                _configurationStore.Save();
            }
        }

        // Deliberately the SAME call the join uses, not a re-derivation of it. Two expressions that
        // are meant to agree drift; one that is shared cannot disagree with itself. A-1.2g asserts
        // on what LEAVES THE CLIENT rather than on what this line says, which is the right way
        // round — this is a preview, and a preview is not evidence.
        var effective = settings.DisplayNameOr(characterName);
        ImGui.TextUnformatted($"You will join as: {effective.Value}");

        if (settings.DisplayNameAlias.Length > 0 && !DisplayName.TryParse(settings.DisplayNameAlias, out _))
        {
            ImGui.TextWrapped(UnusableAliasWarning);
        }

        ImGui.TextWrapped(NameIsNotIdentity);
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

        if (!RelayEndpoint.TryParse(settings.RelayAddress, out _))
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
