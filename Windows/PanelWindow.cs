using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The plugin's one window: a rail on its left edge whose buttons switch the tab shown beside it.</summary>
internal sealed class PanelWindow : ThemedWindow
{
    private static readonly Vector2 MinimumSize = new(480, 360);

    private static readonly Vector2 FirstSize = new(640, 560);

    private readonly ConfigurationStore _configurationStore;
    private readonly SessionCoordinator _coordinator;
    private ChatTab? _chat;
    private SessionTab? _session;
    private SettingsTab? _settings;

    private Vector2 _expandedSize = FirstSize;
    private bool _restoreSize;

    public PanelWindow(ConfigurationStore configurationStore, SessionCoordinator coordinator, UiFonts fonts, IPluginLog log)
        : base("Dungeon Master XIV###dmx-main", fonts, log)
    {
        _configurationStore = configurationStore;
        _coordinator = coordinator;
        RespectCloseHotkey = false;

        IsOpen = Settings.ShouldOpenOnLoad(Settings.MainWindowOpen);
    }

    private PluginSettings Settings => _configurationStore.Configuration.Settings;

    /// <summary>Supplies the tabs once they exist; the Chat tab needs this window to switch to Session.</summary>
    public void Attach(ChatTab chat, SessionTab session, SettingsTab settings)
    {
        _chat = chat;
        _session = session;
        _settings = settings;
    }

    /// <summary>Opens the panel, expanded, on one tab.</summary>
    public void Show(PanelTab tab)
    {
        IsOpen = true;
        Select(tab);
        SetCollapsed(false);
    }

    public void Select(PanelTab tab)
    {
        if (Settings.RecordSelectedTab(tab))
        {
            _configurationStore.Save();
        }
    }

    public override void PreDraw()
    {
        var baseFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse;
        if (Settings.PanelCollapsed)
        {
            Flags = baseFlags | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize;
            SizeConstraints = null;
            Size = null;
        }
        else
        {
            Flags = baseFlags;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = MinimumSize, MaximumSize = new Vector2(float.MaxValue) };
            Size = _restoreSize ? _expandedSize : null;
            SizeCondition = ImGuiCond.Always;
            _restoreSize = false;
        }

        base.PreDraw();
    }

    public override void OnOpen() => Remember(true);

    public override void OnClose() => Remember(false);

    protected override void DrawContent()
    {
        if (Settings.PanelCollapsed)
        {
            DrawRail(collapsed: true);
            return;
        }

        _expandedSize = ImGui.GetWindowSize() / ImGuiHelpers.GlobalScale;

        var railWidth = RailButton.Size + (ImGui.GetStyle().WindowPadding.X * 2f);
        using (var rail = ImRaii.Child("##rail", new Vector2(railWidth, 0f), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (rail.Success)
            {
                DrawRail(collapsed: false);
            }
        }

        ImGui.SameLine();
        using var tab = ImRaii.Child("##tab", Vector2.Zero, false);
        if (!tab.Success)
        {
            return;
        }

        switch (Settings.SelectedTab)
        {
            case PanelTab.Chat:
                _chat?.Draw();
                break;
            case PanelTab.Settings:
                _settings?.Draw();
                break;
            default:
                _session?.Draw();
                break;
        }
    }

    private void DrawRail(bool collapsed)
    {
        var selected = collapsed ? (PanelTab?)null : Settings.SelectedTab;

        if (RailButton.Draw(Fonts, FontAwesomeIcon.Comments, "Chat", selected == PanelTab.Chat))
        {
            Show(PanelTab.Chat);
        }

        var waiting = _coordinator.InAHostedSession ? _coordinator.Admissions.Pending.Count : 0;
        if (RailButton.Draw(Fonts, FontAwesomeIcon.Users, "Session", selected == PanelTab.Session, waiting))
        {
            Show(PanelTab.Session);
        }

        if (!collapsed)
        {
            var bottom = (RailButton.Size * 2f) + ImGui.GetStyle().ItemSpacing.Y;
            var space = ImGui.GetContentRegionAvail().Y - bottom;
            if (space > 0f)
            {
                ImGui.Dummy(new Vector2(0f, space));
            }
        }

        if (RailButton.Draw(Fonts, FontAwesomeIcon.Cog, "Settings", selected == PanelTab.Settings))
        {
            Show(PanelTab.Settings);
        }

        if (collapsed)
        {
            if (RailButton.Draw(Fonts, FontAwesomeIcon.ChevronRight, "Expand", lit: false))
            {
                SetCollapsed(false);
            }
        }
        else if (RailButton.Draw(Fonts, FontAwesomeIcon.ChevronLeft, "Collapse to the rail", lit: false))
        {
            SetCollapsed(true);
        }
    }

    private void SetCollapsed(bool collapsed)
    {
        if (!Settings.RecordPanelCollapsed(collapsed))
        {
            return;
        }

        _configurationStore.Save();
        _restoreSize = !collapsed;
    }

    private void Remember(bool isOpen)
    {
        if (Settings.RecordMainWindowOpen(isOpen))
        {
            _configurationStore.Save();
        }
    }
}
