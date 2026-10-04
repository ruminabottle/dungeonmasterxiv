using System.Numerics;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A small outlined tag for a session role: "[DM]" for the host, "Assistant", nothing for a player.</summary>
internal static class RoleBadge
{
    /// <summary>The badge text, or null when the role has none. Only the host's is bracketed (rolls R-2.7a).</summary>
    public static string? TextFor(SessionRole role) => role switch
    {
        SessionRole.DungeonMaster => "[DM]",
        SessionRole.Assistant => "Assistant",
        _ => null,
    };

    /// <summary>Draws the badge and returns true, or draws nothing and returns false.</summary>
    public static bool Draw(UiFonts fonts, SessionRole role)
    {
        if (TextFor(role) is not { } text)
        {
            return false;
        }

        using var font = fonts.Meta.Push();
        var padding = new Vector2(Metrics.Step, 1f);
        var size = ImGui.CalcTextSize(text) + (padding * 2f);
        var start = ImGui.GetCursorScreenPos();

        ImGui.GetWindowDrawList().AddRect(start, start + size, ImGui.GetColorU32(Palette.GoldLabel), Metrics.ControlRounding);
        ImGui.SetCursorScreenPos(start + padding);
        ImGui.TextColored(Palette.GoldLabel, text);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(size);
        return true;
    }
}
