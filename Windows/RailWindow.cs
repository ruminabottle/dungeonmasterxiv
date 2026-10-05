using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>One rail button: the window it toggles, its icon, and its tooltip.</summary>
internal sealed record RailEntry(Window Window, FontAwesomeIcon Icon, string Tooltip);

/// <summary>The main window: a movable column of buttons, one per window that exists, lit while it is open.</summary>
internal sealed class RailWindow : ThemedWindow
{
    private readonly ConfigurationStore _configurationStore;
    private readonly IReadOnlyList<RailEntry> _entries;
    private readonly RailEntry _settings;
    private readonly Window _introduceBeside;

    public RailWindow(
        ConfigurationStore configurationStore,
        UiFonts fonts,
        IPluginLog log,
        IReadOnlyList<RailEntry> entries,
        RailEntry settings,
        Window introduceBeside)
        : base(
            "Dungeon Master XIV###dmx-main",
            fonts,
            log,
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse)
    {
        _configurationStore = configurationStore;
        _entries = entries;
        _settings = settings;
        _introduceBeside = introduceBeside;
        RespectCloseHotkey = false;

        IsOpen = configurationStore.Configuration.Settings.ShouldOpenOnLoad(
            configurationStore.Configuration.Settings.MainWindowOpen);
    }

    private PluginSettings Settings => _configurationStore.Configuration.Settings;

    protected override void DrawContent()
    {
        DrawGrip();

        if (Settings.RailCollapsed)
        {
            if (RailButton.Draw(Fonts, FontAwesomeIcon.DiceD20, "Show the Dungeon Master XIV buttons", lit: false))
            {
                Collapse(false);
            }

            return;
        }

        foreach (var entry in _entries)
        {
            DrawEntry(entry);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Step * 2f));
        DrawEntry(_settings);

        if (RailButton.Draw(Fonts, FontAwesomeIcon.ChevronLeft, "Collapse to one button", lit: false))
        {
            Collapse(true);
        }

        IntroduceOnce();
    }

    public override void OnOpen() => Remember(true);

    public override void OnClose() => Remember(false);

    private void DrawEntry(RailEntry entry)
    {
        if (RailButton.Draw(Fonts, entry.Icon, entry.Tooltip, entry.Window.IsOpen))
        {
            entry.Window.Toggle();
        }
    }

    /// <summary>A handle to drag the rail by, since it has no title bar.</summary>
    private void DrawGrip()
    {
        using var icon = Fonts.Icon.Push();
        var glyph = FontAwesomeIcon.GripLines.ToIconString();
        var glyphSize = ImGui.CalcTextSize(glyph);
        var size = new Vector2(RailButton.Size, glyphSize.Y + Metrics.Step);

        ImGui.InvisibleButton("##grip", size);
        var min = ImGui.GetItemRectMin();
        ImGui.GetWindowDrawList().AddText(
            min + ((size - glyphSize) / 2f), ImGui.GetColorU32(ImGui.IsItemHovered() ? Palette.Gold : Palette.Rule), glyph);

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        }
    }

    private void Collapse(bool collapsed)
    {
        if (Settings.RecordRailCollapsed(collapsed))
        {
            _configurationStore.Save();
        }
    }

    /// <summary>The first time the rail ever opens, the session window opens beside it.</summary>
    private void IntroduceOnce()
    {
        if (Settings.RailIntroduced)
        {
            return;
        }

        _introduceBeside.Position = ImGui.GetWindowPos() + new Vector2(ImGui.GetWindowSize().X + (Metrics.Step * 2f), 0f);
        _introduceBeside.PositionCondition = ImGuiCond.FirstUseEver;
        _introduceBeside.IsOpen = true;
        Settings.RailIntroduced = true;
        _configurationStore.Save();
    }

    private void Remember(bool isOpen)
    {
        if (Settings.RecordMainWindowOpen(isOpen))
        {
            _configurationStore.Save();
        }
    }
}
