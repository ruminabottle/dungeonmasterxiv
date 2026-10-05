using System.Numerics;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A membership change as one compact muted line, or a gap as a rule with a note.</summary>
internal static class EventLine
{
    public const string GapText = "Some messages were not held.";

    public static string? TextFor(StreamEventKind kind, string name) => kind switch
    {
        StreamEventKind.Joined => $"{name} joined",
        StreamEventKind.Left => $"{name} left",
        StreamEventKind.Dropped => $"{name} lost connection",
        StreamEventKind.Reconnected => $"{name} reconnected",
        _ => null,
    };

    public static void Draw(UiFonts fonts, StreamEventKind kind, SpeakerName speaker, long atUtcTicks)
    {
        using var font = fonts.Meta.Push();

        if (kind == StreamEventKind.Gap)
        {
            DrawGap();
            return;
        }

        if (TextFor(kind, speaker.Name) is not { } text)
        {
            return;
        }

        ImGui.TextColored(Palette.TextMuted, $"{text} · {Speaker.TimeOf(atUtcTicks)}");
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
    }

    private static void DrawGap()
    {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var middle = start.Y + (ImGui.GetTextLineHeight() / 2f);
        var textWidth = ImGui.CalcTextSize(GapText).X;
        var textStart = start.X + ((width - textWidth) / 2f);
        var colour = ImGui.GetColorU32(Palette.RuleSoft);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddLine(new Vector2(start.X, middle), new Vector2(textStart - Metrics.Step, middle), colour);
        drawList.AddLine(new Vector2(textStart + textWidth + Metrics.Step, middle), new Vector2(start.X + width, middle), colour);
        ImGui.SetCursorScreenPos(new Vector2(textStart, start.Y));
        ImGui.TextColored(Palette.TextMuted, GapText);
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
    }
}
