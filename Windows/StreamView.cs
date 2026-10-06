using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
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
    private readonly Dictionary<long, float> _lineHeights = new();
    private readonly Dictionary<LocalRoll, float> _localHeights = new();
    private float _heightsWidth;
    private float _heightsScale;
    private int _lastCount;
    private bool _newBelow;
    private bool _drewAfter;

    public StreamView(SessionCoordinator coordinator, UiFonts fonts)
    {
        _coordinator = coordinator;
        _fonts = fonts;
    }

    /// <summary>Draws the stream, then <paramref name="drawAfter"/> (true when it drew) as the last thing in it.</summary>
    public void Draw(float height, SpeakerName you, IReadOnlyList<LocalRoll> localRolls, Func<bool>? drawAfter = null)
    {
        var total = _coordinator.StreamCount;
        var lines = total > 0 ? _coordinator.LatestStreamLines(MostShown) : [];

        using var child = ImRaii.Child("##stream", new Vector2(0f, height), false);
        if (!child.Success)
        {
            return;
        }

        var atBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f;
        var count = total + localRolls.Count;

        if (count < _lastCount)
        {
            _lastCount = 0;
            _newBelow = false;
            _lineHeights.Clear();
            _localHeights.Clear();
        }

        var width = ImGui.GetContentRegionAvail().X;
        var scale = ImGuiHelpers.GlobalScale;
        if (width != _heightsWidth || scale != _heightsScale)
        {
            _heightsWidth = width;
            _heightsScale = scale;
            _lineHeights.Clear();
            _localHeights.Clear();
        }

        if (total > MostShown)
        {
            using var meta = _fonts.Meta.Push();
            ImGui.TextColored(Palette.TextMuted, $"Showing the latest {MostShown} entries.");
        }

        foreach (var line in lines)
        {
            if (SkippedOffScreen(_lineHeights, line.Sequence))
            {
                continue;
            }

            var top = ImGui.GetCursorPosY();
            DrawLine(line);
            _lineHeights[line.Sequence] = DrawnHeight(top);
        }

        foreach (var local in localRolls)
        {
            if (SkippedOffScreen(_localHeights, local))
            {
                continue;
            }

            var top = ImGui.GetCursorPosY();
            RollCard.Draw(_fonts, you, local.AtUtcTicks, local.Roll, local: true);
            _localHeights[local] = DrawnHeight(top);
        }

        var drewAfter = drawAfter?.Invoke() ?? false;
        if (drewAfter && !_drewAfter && atBottom)
        {
            ImGui.SetScrollHereY(1f);
        }

        _drewAfter = drewAfter;

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

    /// <summary>Stands in for an entry known to be off-screen with space of its last drawn height.</summary>
    private static bool SkippedOffScreen<TKey>(Dictionary<TKey, float> heights, TKey key)
        where TKey : notnull
    {
        if (!heights.TryGetValue(key, out var height)
            || ImGui.IsRectVisible(new Vector2(ImGui.GetContentRegionAvail().X, height)))
        {
            return false;
        }

        ImGui.Dummy(new Vector2(0f, height));
        return true;
    }

    private static float DrawnHeight(float top) => ImGui.GetCursorPosY() - top - ImGui.GetStyle().ItemSpacing.Y;

    private void DrawLine(StreamLine line)
    {
        using var id = ImRaii.PushId(line.Sequence.ToString(CultureInfo.InvariantCulture));
        var speaker = _coordinator.Speakers.For(line.Peer);

        if (line.Withheld == true)
        {
            PrivateCard.Placeholder(_fonts, speaker, line.AtUtcTicks, line.Text);
            return;
        }

        var mark = MarkFor(line);

        switch (line.Kind)
        {
            case StreamEventKind.Message:
                MessageCard.Draw(_fonts, speaker, line.AtUtcTicks, line.Text, mark);
                break;
            case StreamEventKind.Roll when line.Roll is { } roll:
                var hideTotal = line.Audience == AudienceKind.Blind
                    && line.RevealedBy is null
                    && !_coordinator.InAHostedSession;
                var canReveal = _coordinator.InAHostedSession && mark is { Revealed: false };
                if (RollCard.Draw(_fonts, speaker, line.AtUtcTicks, roll, mark: mark, hideTotal: hideTotal, canReveal: canReveal))
                {
                    _coordinator.Reveal(line.Sequence, DateTimeOffset.UtcNow);
                }

                break;
            case StreamEventKind.Roll:
                MessageCard.Draw(_fonts, speaker, line.AtUtcTicks, line.Text, mark);
                break;
            default:
                EventLine.Draw(_fonts, line.Kind, speaker, line.AtUtcTicks);
                break;
        }
    }

    private AudienceMark? MarkFor(StreamLine line)
    {
        if (line.Audience is not { } kind || kind == AudienceKind.Public)
        {
            return null;
        }

        return kind == AudienceKind.Player && line.To is { } to
            ? new AudienceMark(FontAwesomeIcon.User, _coordinator.Speakers.For(to).Name, false, line.RevealedBy is not null)
            : new AudienceMark(FontAwesomeIcon.UserSecret, "DM", kind == AudienceKind.Blind, line.RevealedBy is not null);
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
