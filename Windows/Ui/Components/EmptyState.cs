using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>What will appear in an empty place and how to make it appear.</summary>
internal static class EmptyState
{
    public static void Draw(UiFonts fonts, string heading, string howTo)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Step * 2f));
        using (fonts.Title.Push())
        {
            ImGui.TextColored(Palette.GoldLabel, heading);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, Palette.TextMuted);
        ImGui.TextWrapped(howTo);
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(0f, Metrics.Step * 2f));
    }
}
