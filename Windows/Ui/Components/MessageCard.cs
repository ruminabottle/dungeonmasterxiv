using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A chat message: speaker and time on top, then the text.</summary>
internal static class MessageCard
{
    public static void Draw(UiFonts fonts, SpeakerName speaker, long atUtcTicks, string text)
    {
        using var card = Card.Begin(Palette.SurfaceRaised, Palette.Rule);
        Speaker.DrawWithTime(fonts, speaker, atUtcTicks);
        ImGui.TextWrapped(text);
    }
}
