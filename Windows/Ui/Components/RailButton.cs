using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A square icon button with a tooltip, lit with a gold edge while its tab is shown, with an optional count badge.</summary>
internal static class RailButton
{
    public static float Size => 34f * ImGuiHelpers.GlobalScale;

    public static bool Draw(UiFonts fonts, FontAwesomeIcon icon, string tooltip, bool lit, int badge = 0)
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

        if (badge > 0)
        {
            DrawBadge(fonts, badge);
        }

        return pressed;
    }

    /// <summary>A gold count on the button's top-right corner; the count is the signal, not only the colour.</summary>
    private static void DrawBadge(UiFonts fonts, int count)
    {
        using var font = fonts.Meta.Push();
        var text = count > 9 ? "9+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var textSize = ImGui.CalcTextSize(text);
        var radius = MathF.Max(textSize.X, textSize.Y) / 2f + 2f;
        var corner = new Vector2(ImGui.GetItemRectMax().X - radius / 2f, ImGui.GetItemRectMin().Y + radius / 2f);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddCircleFilled(corner, radius, ImGui.GetColorU32(Palette.Gold));
        drawList.AddText(corner - (textSize / 2f), ImGui.GetColorU32(Palette.Surface), text);
    }
}
