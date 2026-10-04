using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>A roll this client made outside a session, shown only here.</summary>
internal sealed record LocalRoll(long AtUtcTicks, SharedRoll Roll);

/// <summary>The session stream as cards and event lines, pinned to the newest entry unless scrolled up.</summary>
internal sealed class StreamView
{
    /// <summary>The most entries drawn each frame; older ones stay in the session log.</summary>
    public const int MostShown = 300;

    private readonly SessionCoordinator _coordinator;
    private readonly UiFonts _fonts;
    private readonly SpeakerBook _speakers = new();
    private int _lastCount;
    private bool _newBelow;

    public StreamView(SessionCoordinator coordinator, UiFonts fonts)
    {
        _coordinator = coordinator;
        _fonts = fonts;
    }

    public void Draw(float height, SpeakerName you, IReadOnlyList<LocalRoll> localRolls)
    {
        _speakers.Learn(_coordinator.CurrentRoster);
        var lines = _coordinator.InASession ? _coordinator.StreamLines : [];

        using var child = ImRaii.Child("##stream", new Vector2(0f, height), false);
        if (!child.Success)
        {
            return;
        }

        var atBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f;
        var count = lines.Count + localRolls.Count;

        if (lines.Count > MostShown)
        {
            using var meta = _fonts.Meta.Push();
            ImGui.TextColored(Palette.TextMuted, $"Showing the latest {MostShown} entries.");
        }

        foreach (var line in lines.Skip(Math.Max(0, lines.Count - MostShown)))
        {
            DrawLine(line);
        }

        foreach (var local in localRolls)
        {
            RollCard.Draw(_fonts, you, local.AtUtcTicks, local.Roll, local: true);
        }

        if (count > _lastCount)
        {
            if (atBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
            else
            {
                _newBelow = true;
            }
        }

        if (atBottom)
        {
            _newBelow = false;
        }

        _lastCount = count;

        if (_newBelow)
        {
            DrawNewBelow();
        }
    }

    private void DrawLine(StreamLine line)
    {
        var speaker = _speakers.For(line.Peer);

        switch (line.Kind)
        {
            case StreamEventKind.Message:
                MessageCard.Draw(_fonts, speaker, line.AtUtcTicks, line.Text);
                break;
            case StreamEventKind.Roll when line.Roll is { } roll:
                RollCard.Draw(_fonts, speaker, line.AtUtcTicks, roll);
                break;
            case StreamEventKind.Roll:
                MessageCard.Draw(_fonts, speaker, line.AtUtcTicks, line.Text);
                break;
            default:
                EventLine.Draw(_fonts, line.Kind, speaker, line.AtUtcTicks);
                break;
        }
    }

    private void DrawNewBelow()
    {
        var label = "New below ↓";
        var width = ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2f);
        var scroll = new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
        var region = ImGui.GetWindowSize();
        ImGui.SetCursorPos(scroll + new Vector2((region.X - width) / 2f, region.Y - ImGui.GetFrameHeight() - Metrics.Step));

        if (ActionRow.Primary(label))
        {
            ImGui.SetScrollHereY(1f);
            _newBelow = false;
        }
    }
}
