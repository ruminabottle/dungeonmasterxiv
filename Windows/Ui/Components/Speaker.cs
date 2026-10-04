using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>Who said something, on one line: the host's "[DM]" badge first, then the name, never shortened.</summary>
internal static class Speaker
{
    public static void Draw(UiFonts fonts, SpeakerName speaker)
    {
        if (speaker.Role == SessionRole.DungeonMaster && RoleBadge.Draw(fonts, speaker.Role))
        {
            ImGui.SameLine();
        }

        ImGui.TextColored(Palette.Text, speaker.Name);

        if (speaker.Role == SessionRole.Assistant)
        {
            ImGui.SameLine();
            RoleBadge.Draw(fonts, speaker.Role);
        }
    }

    /// <summary>The speaker line with a right-aligned local time, as cards and event lines use it.</summary>
    public static void DrawWithTime(UiFonts fonts, SpeakerName speaker, long atUtcTicks)
    {
        Draw(fonts, speaker);
        DrawTime(fonts, atUtcTicks);
    }

    public static string TimeOf(long atUtcTicks) =>
        new DateTimeOffset(atUtcTicks, TimeSpan.Zero).ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

    private static void DrawTime(UiFonts fonts, long atUtcTicks)
    {
        using var font = fonts.Meta.Push();
        var time = TimeOf(atUtcTicks);
        var width = ImGui.CalcTextSize(time).X;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - width - Metrics.CardPadding);
        ImGui.TextColored(Palette.TextMuted, time);
    }
}
