using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>The button vocabulary: primary with a gold edge, secondary with a strong rule edge.</summary>
internal static class ActionRow
{
    public static bool Primary(string label) => Edged(label, Palette.Gold, Palette.Text);

    public static bool Secondary(string label) => Edged(label, Palette.RuleStrong, Palette.Text);

    /// <summary>A disabled button with a tooltip saying why it cannot be pressed.</summary>
    public static void Unavailable(string label, string why)
    {
        using (ImRaii.Disabled())
        {
            Edged(label, Palette.RuleStrong, Palette.Text);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(why);
        }
    }

    internal static bool Edged(string label, Vector4 edge, Vector4 text)
    {
        using var colours = ImRaii.PushColor(ImGuiCol.Border, edge).Push(ImGuiCol.Text, text);
        return ImGui.Button(label);
    }
}

/// <summary>A danger button that asks for confirmation on the first press and acts on the second.</summary>
internal sealed class DangerAction
{
    private bool _armed;

    /// <summary>Returns true once the person has confirmed.</summary>
    public bool Draw(string label, string confirmLabel, string cancelLabel = "Cancel")
    {
        if (!_armed)
        {
            if (ActionRow.Edged(label, Palette.Danger, Palette.Danger))
            {
                _armed = true;
            }

            return false;
        }

        var confirmed = ActionRow.Edged(confirmLabel, Palette.Danger, Palette.Danger);
        ImGui.SameLine();
        if (ActionRow.Secondary(cancelLabel) || confirmed)
        {
            _armed = false;
        }

        return confirmed;
    }
}
