using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>One person at the table: name, role badge, and "away" while disconnected. Never a role in parentheses.</summary>
internal static class RosterRow
{
    public const string Away = "away";

    public static void Draw(UiFonts fonts, SpeakerName person, bool away = false)
    {
        ImGui.TextColored(Palette.Text, person.Name);

        if (RoleBadge.TextFor(person.Role) is not null)
        {
            ImGui.SameLine();
            RoleBadge.Draw(fonts, person.Role);
        }

        if (away)
        {
            ImGui.SameLine();
            using var meta = fonts.Meta.Push();
            ImGui.TextColored(Palette.TextMuted, Away);
        }
    }
}
