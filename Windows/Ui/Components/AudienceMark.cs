using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>The line under a private card's speaker: who it is for, blind, and revealed, as icons with short words.</summary>
internal sealed record AudienceMark(FontAwesomeIcon Icon, string Label, bool Blind, bool Revealed)
{
    public const string RevealedByTheDm = "Revealed by the DM";

    public void Draw(UiFonts fonts)
    {
        using var meta = fonts.Meta.Push();
        Glyph(fonts, Icon);
        ImGui.SameLine();
        ImGui.TextColored(Palette.PrivateText, Label);

        if (Blind)
        {
            ImGui.SameLine();
            Glyph(fonts, FontAwesomeIcon.EyeSlash);
        }

        if (Revealed)
        {
            ImGui.SameLine();
            Glyph(fonts, FontAwesomeIcon.Eye);
            ImGui.SameLine();
            ImGui.TextColored(Palette.TextMuted, RevealedByTheDm);
        }
    }

    private static void Glyph(UiFonts fonts, FontAwesomeIcon icon)
    {
        using var font = fonts.Icon.Push();
        ImGui.TextColored(Palette.PrivateText, icon.ToIconString());
    }
}
