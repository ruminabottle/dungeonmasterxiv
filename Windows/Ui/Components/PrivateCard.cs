using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>What someone not entitled to a private roll sees: the speaker, one line of text, and a "?" bar.</summary>
internal static class PrivateCard
{
    public static void Placeholder(UiFonts fonts, SpeakerName speaker, long atUtcTicks, string text)
    {
        using var card = Card.Begin(Palette.PrivateSurface, Palette.PrivateRule);
        Speaker.DrawWithTime(fonts, speaker, atUtcTicks);
        ImGui.TextColored(Palette.PrivateText, text);
        RollCard.Bar(fonts.Total, "?", Palette.GoldBright);
    }
}
