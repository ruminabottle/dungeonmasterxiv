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
        Func<string, bool> supports)
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
            ImGui.SameLine();
            ImGui.TextColored(Palette.PrivateText, NameOf(roster, to));
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
                if (ImGui.Selectable($"{NameOf(roster, player.PeerCode)}##{player.PeerCode}"))
                {
                    choice.ChoosePlayer(player.PeerCode);
                }
            }

            if (!ready && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(NeedsUpdating(NameOf(roster, player.PeerCode)));
            }
        }
    }

    public static string NeedsUpdating(string name) => $"{name}'s plugin needs updating for private messages";

    public static string NameOf(IReadOnlyList<RosterEntry> roster, string peerCode) =>
        roster.FirstOrDefault(entry => entry.PeerCode == peerCode) is { PeerCode: not null } entry
            ? DisplayName.OrNone(entry.DisplayName).Value
            : DisplayName.Unstated;
}
