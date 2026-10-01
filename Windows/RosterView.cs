using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>Draws a list of session participants by name, with a role label where one applies.</summary>
internal static class RosterView
{
    public static void Draw(IEnumerable<(string Name, SessionRole Role)> participants)
    {
        foreach (var (name, role) in participants)
        {
            ImGui.TextUnformatted(
                SessionRoleLabel.For(role) is { } label ? $"  {name} ({label})" : $"  {name}");
        }
    }
}
