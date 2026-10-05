using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A square icon button with a tooltip, lit with a gold edge while its window is open.</summary>
internal static class RailButton
{
    public static float Size => 34f * ImGuiHelpers.GlobalScale;

    public static bool Draw(UiFonts fonts, FontAwesomeIcon icon, string tooltip, bool lit)
    {
        bool pressed;
        using (ImRaii.PushColor(ImGuiCol.Button, lit ? Palette.SurfaceRaised : Palette.Surface)
                   .Push(ImGuiCol.Border, lit ? Palette.Gold : Palette.Surface)
                   .Push(ImGuiCol.Text, lit ? Palette.GoldBright : Palette.GoldLabel))
        using (fonts.Icon.Push())
        {
            pressed = ImGui.Button($"{icon.ToIconString()}##{tooltip}", new Vector2(Size, Size));
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }

        return pressed;
    }
}
