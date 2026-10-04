using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The settings window: window restore, display name, relay address, the relay policy link, and campaign and user storage.</summary>
internal sealed class ConfigWindow : ThemedWindow
{
    private readonly ConfigurationStore _configurationStore;
    private readonly Func<DisplayName> _characterName;

    private readonly Func<Campaign?> _currentCampaign;
    private readonly Action<Campaign> _saveCampaign;

    private readonly string _schemaVersionLabel;
    private readonly RelinkMemoryView _relinkMemory;
    private readonly CampaignStorageView _campaignStorage;

    private static readonly string UnusableAliasWarning =
        $"This name cannot be used, so your character name will be sent instead. Names are limited "
        + $"to {DisplayName.MaxLength} characters and cannot contain line breaks or invisible "
        + "formatting characters.";

    private const string RelayPolicyUrl =
        "https://github.com/ruminabottle/dungeonmasterxiv/blob/main/RELAY-SERVICE-POLICY.md";

    private const string NameFieldIsFull =
        "This box is full and will not take any more. If you were still typing, the rest did not go "
        + "in - use a shorter name.";

    private const string NameNeedsACampaign =
        "A name is saved with a campaign, and no campaign is open. Until you open or create one, "
        + "you will join as your character name and this box cannot be changed.";

    private const string InvalidRelayWarning =
        "This is not a usable relay address. It must start with wss:// - or ws:// for a relay "
        + "running on this machine.";

    public ConfigWindow(
        ConfigurationStore configurationStore,
        Func<DisplayName> characterName,
        Func<Campaign?> currentCampaign,
        Action<Campaign> saveCampaign,
        CampaignStorageView campaignStorage,
        UiFonts fonts,
        IPluginLog log)
        : base("Settings###dmx-settings", fonts, log)
    {
        _configurationStore = configurationStore;
        _characterName = characterName;
        _currentCampaign = currentCampaign;
        _saveCampaign = saveCampaign;
        _campaignStorage = campaignStorage;

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

    protected override void DrawContent()
    {
        var settings = _configurationStore.Configuration.Settings;

        Section.Heading(Fonts, "You");
        DrawDisplayNameSetting(settings);

        var restore = settings.RestoreWindowState;
        if (ImGui.Checkbox("Reopen windows where I left them", ref restore))
        {
            settings.RestoreWindowState = restore;
            _configurationStore.Save();
        }

        Section.Heading(Fonts, "Connection");
        DrawRelaySetting(settings);

        if (ActionRow.Secondary("Relay and privacy"))
        {
            Dalamud.Utility.Util.OpenLink(RelayPolicyUrl);
        }

        Section.Heading(Fonts, "Campaigns");
        _campaignStorage.Draw();

        Section.Heading(Fonts, "Stored data");
        _relinkMemory.Draw();

        ImGui.Spacing();
        using (Fonts.Meta.Push())
        {
            ImGui.TextColored(Palette.TextMuted, _schemaVersionLabel);
        }
    }

    private void DrawDisplayNameSetting(PluginSettings settings)
    {

        var characterName = _characterName();

        var campaign = _currentCampaign();
        var typed = DrawNameBox(campaign, settings.DisplayNameAlias, characterName);

        if (NameInputCapacity.IsFull(typed))
        {
            Banner.Draw(Fonts, BannerKind.Warning, NameFieldIsFull);
        }

        var effective = CampaignDisplayName.Or(campaign, characterName);
        ImGui.TextUnformatted($"You will join as: {effective.Value}");

        var stored = CampaignDisplayName.Stored(campaign);
        if (stored.Length > 0 && !DisplayName.TryParse(stored, out _))
        {
            Banner.Draw(Fonts, BannerKind.Warning, UnusableAliasWarning);
        }
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
            Banner.Draw(Fonts, BannerKind.Info, NameNeedsACampaign);
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
        var address = settings.RelayAddress;
        if (ImGui.InputText("Relay address", ref address, 256))
        {
            settings.RelayAddress = address;
            _configurationStore.Save();
        }

        if (!RelayEndpoint.TryParse(settings.RelayAddress, out _))
        {
            Banner.Draw(Fonts, BannerKind.Danger, InvalidRelayWarning);
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
