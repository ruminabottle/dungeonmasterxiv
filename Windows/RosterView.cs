using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

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
