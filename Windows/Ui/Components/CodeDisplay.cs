using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>The session code, large, with a Copy button (session-layer R-1.3i).</summary>
internal static class CodeDisplay
{
    public static void Draw(UiFonts fonts, SessionCode code)
    {
        using (fonts.Meta.Push())
        {
            ImGui.TextColored(Palette.TextMuted, "Session code");
        }

        using (fonts.Code.Push())
        {
            ImGui.TextColored(Palette.GoldBright, code.ToDisplayString());
        }

        ImGui.SameLine();
        if (ActionRow.Secondary("Copy"))
        {
            ImGui.SetClipboardText(code.ToClipboardString());
        }
    }
}
