using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>How serious a banner is: its edge colour and icon.</summary>
internal enum BannerKind
{
    Info,
    Warning,
    Danger,
}

/// <summary>A status note with a coloured left edge and an icon, so its kind never rests on colour alone.</summary>
internal static class Banner
{
    public static void Draw(UiFonts fonts, BannerKind kind, string text)
    {
        var (colour, icon) = kind switch
        {
            BannerKind.Warning => (Palette.Warning, FontAwesomeIcon.ExclamationTriangle),
            BannerKind.Danger => (Palette.Danger, FontAwesomeIcon.TimesCircle),
            _ => (Palette.Gold, FontAwesomeIcon.InfoCircle),
        };

        using var card = Card.WithLeftEdge(Palette.SurfaceRaised, Palette.RuleSoft, colour);
        using (fonts.Icon.Push())
        {
            ImGui.TextColored(colour, icon.ToIconString());
        }

        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }

    /// <summary>A one-line refusal under an input, in the danger colour.</summary>
    public static void Refusal(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Palette.Danger);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
    }
}
