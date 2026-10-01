using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>The settings window: window restore, display name, relay address, privacy notes and relink memory.</summary>
public sealed class ConfigWindow : Window
{
    private readonly ConfigurationStore _configurationStore;
    private readonly Func<DisplayName> _characterName;

    private readonly Func<Campaign?> _currentCampaign;
    private readonly Action<Campaign> _saveCampaign;

    private readonly string _schemaVersionLabel;
    private readonly RelinkMemoryView _relinkMemory;

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

    private static readonly string UnusableAliasWarning =
        $"This name cannot be used, so your character name will be sent instead. Names are limited "
        + $"to {DisplayName.MaxLength} characters and cannot contain line breaks or invisible "
        + "formatting characters - they are shown next to the code you compare, and a name that can "
        + "redraw that line is a way to hide it.";

    private const string NameFieldIsFull =
        "This box is full and will not take any more. If you were still typing, the rest did not go "
        + "in - use a shorter name.";

    private const string NameNeedsACampaign =
        "A name is saved with a campaign, and no campaign is open. Until you open or create one, "
        + "you will join as your character name and this box cannot be changed.";

    private const string NameIsNotIdentity =
        "This name is not checked by anything. Anyone can send any name, so it tells your DM who "
        + "you say you are and nothing more - the code you read to each other is the part that "
        + "proves anything.";

    private const string InvalidRelayWarning =
        "This is not a usable relay address. It must start with wss:// - or ws:// for a relay "
        + "running on this machine.";

    public ConfigWindow(
        ConfigurationStore configurationStore,
        Func<DisplayName> characterName,
        Func<Campaign?> currentCampaign,
        Action<Campaign> saveCampaign)
        : base("Dungeon Master XIV settings###dmx-settings")
    {
        _configurationStore = configurationStore;
        _characterName = characterName;
        _currentCampaign = currentCampaign;
        _saveCampaign = saveCampaign;

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

    public void Open() => IsOpen = true;

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

        ImGui.Separator();
        ImGui.TextUnformatted("What this plugin remembers about you");
        _relinkMemory.Draw();

        ImGui.Separator();
        ImGui.TextDisabled(_schemaVersionLabel);
    }

    private void DrawDisplayNameSetting(PluginSettings settings)
    {
        ImGui.TextUnformatted("Display name");

        var characterName = _characterName();

        var campaign = _currentCampaign();
        var typed = DrawNameBox(campaign, settings.DisplayNameAlias, characterName);

        if (NameInputCapacity.IsFull(typed))
        {
            ImGui.TextWrapped(NameFieldIsFull);
        }

        var effective = CampaignDisplayName.Or(campaign, characterName);
        ImGui.TextUnformatted($"You will join as: {effective.Value}");

        var stored = CampaignDisplayName.Stored(campaign);
        if (stored.Length > 0 && !DisplayName.TryParse(stored, out _))
        {
            ImGui.TextWrapped(UnusableAliasWarning);
        }

        ImGui.TextWrapped(NameIsNotIdentity);
    }

    private string DrawNameBox(Campaign? campaign, string? carriedOverDefault, DisplayName characterName)
    {
        var noCampaign = campaign is null;

        var typed = CampaignDisplayName.ToPreFill(campaign, carriedOverDefault, characterName);

        if (noCampaign)
        {
            ImGui.BeginDisabled();
        }

        var edited = ImGui.InputText("Name others see", ref typed, DisplayName.MaxUtf8Bytes);

        if (noCampaign)
        {
            ImGui.EndDisabled();
            ImGui.TextWrapped(NameNeedsACampaign);
        }

        if (edited)
        {
            if (campaign is not null && CampaignDisplayName.RecordChosen(campaign, typed, characterName))
            {
                _saveCampaign(campaign);
            }
        }

        return typed;
    }

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

    public override void OnOpen() => Remember(true);

    public override void OnClose() => Remember(false);

    private void Remember(bool isOpen)
    {
        if (_configurationStore.Configuration.Settings.RecordSettingsWindowOpen(isOpen))
        {
            _configurationStore.Save();
        }
    }
}
