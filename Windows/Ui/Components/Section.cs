using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A small-caps Cinzel label over a soft rule, optionally collapsible.</summary>
internal static class Section
{
    public static void Heading(UiFonts fonts, string label)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
        using (fonts.Title.Push())
        {
            ImGui.TextColored(Palette.GoldLabel, label.ToUpperInvariant());
        }

        Rule();
    }

    /// <summary>A heading that opens and closes; returns true while open.</summary>
    public static bool Collapsible(UiFonts fonts, string label, bool openByDefault = true)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
        bool open;
        using (fonts.Title.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.GoldLabel)
                   .Push(ImGuiCol.Header, Vector4.Zero)
                   .Push(ImGuiCol.HeaderHovered, Palette.SurfaceHover)
                   .Push(ImGuiCol.HeaderActive, Palette.SurfaceHover))
        {
            open = ImGui.CollapsingHeader(
                label.ToUpperInvariant(), openByDefault ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
        }

        Rule();
        return open;
    }

    private static void Rule()
    {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.GetWindowDrawList().AddLine(start, start + new Vector2(width, 0f), ImGui.GetColorU32(Palette.RuleSoft));
        ImGui.Dummy(new Vector2(0f, Metrics.Step));
    }
}
