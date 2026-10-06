using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>The icon row above the message box that picks who sees the next message or roll.</summary>
internal static class AudienceRow
{
    public const string PublicTooltip = "Public: everyone sees it";
    public const string DmOnlyTooltip = "DM only: you and the DM";
    public const string DmSideTooltip = "DM side only";
    public const string BlindTooltip = "Blind: the DM sees the roll, you don't";
    public const string ToPlayerTooltip = "To one player";

    public static float Height => RailButton.Size + ImGui.GetStyle().ItemSpacing.Y;

    public static void Draw(
        UiFonts fonts,
        AudienceChoice choice,
        bool host,
        IReadOnlyList<RosterEntry> roster,
        Func<string, bool> supports,
        Func<string, string> nameOf)
    {
        if (RailButton.Draw(fonts, FontAwesomeIcon.Globe, PublicTooltip, choice.Kind == AudienceKind.Public))
        {
            choice.Choose(AudienceKind.Public);
        }

        ImGui.SameLine();
        if (RailButton.Draw(fonts, FontAwesomeIcon.UserSecret, host ? DmSideTooltip : DmOnlyTooltip, choice.Kind == AudienceKind.DmSide))
        {
            choice.Choose(AudienceKind.DmSide);
        }

        ImGui.SameLine();
        if (!host)
        {
            if (RailButton.Draw(fonts, FontAwesomeIcon.EyeSlash, BlindTooltip, choice.Kind == AudienceKind.Blind))
            {
                choice.Choose(AudienceKind.Blind);
            }

            return;
        }

        if (RailButton.Draw(fonts, FontAwesomeIcon.User, ToPlayerTooltip, choice.Kind == AudienceKind.Player))
        {
            ImGui.OpenPopup("##to-player");
        }

        if (choice.Kind == AudienceKind.Player && choice.To is { } to)
        {
            var stillAtTheTable = roster.Any(entry => entry.PeerCode == to);
            ImGui.SameLine();
            ImGui.TextColored(stillAtTheTable ? Palette.PrivateText : Palette.TextMuted, nameOf(to));
        }

        using var popup = ImRaii.Popup("##to-player");
        if (!popup.Success)
        {
            return;
        }

        foreach (var player in roster.Where(entry => entry.Role == SessionRole.Player))
        {
            var ready = supports(player.PeerCode);
            using (ImRaii.Disabled(!ready))
            {
                if (ImGui.Selectable($"{nameOf(player.PeerCode)}##{player.PeerCode}"))
                {
                    choice.ChoosePlayer(player.PeerCode);
                }
            }

            if (!ready && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(NeedsUpdating(nameOf(player.PeerCode)));
            }
        }
    }

    public static string NeedsUpdating(string name) => $"{name}'s plugin needs updating for private messages";
}
