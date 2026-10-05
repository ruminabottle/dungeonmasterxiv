using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>Shows the host each pending join request as a card with Admit and Deny.</summary>
internal sealed class AdmissionPromptView
{
    private readonly SessionCoordinator _coordinator;
    private readonly UiFonts _fonts;

    public AdmissionPromptView(SessionCoordinator coordinator, UiFonts fonts)
    {
        _coordinator = coordinator;
        _fonts = fonts;
    }

    public void Draw()
    {
        var pending = _coordinator.Admissions.Pending;
        if (pending.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var request in pending.ToArray())
        {
            using var card = Card.Begin(Palette.SurfaceRaised, Palette.Gold);
            using var id = ImRaii.PushId(request.PeerCode.Value);

            ImGui.TextWrapped(AdmissionPrompt.Headline(request));
            using (_fonts.Meta.Push())
            {
                ImGui.TextColored(Palette.TextMuted, $"This request lapses in {request.RemainingAt(now):mm\\:ss}");
            }

            DrawActions(request);
        }
    }

    private void DrawActions(PendingAdmission request)
    {
        var favoured = AdmissionPrompt.Favoured(request);

        if (_coordinator.CanAdmitAsClaimed(request))
        {
            var label = string.IsNullOrEmpty(request.RelinkLabel) ? "returning player" : request.RelinkLabel;
            if (Button($"Admit as {label}", primary: false))
            {
                _coordinator.Admit(request.PeerCode, asClaimed: true);
            }

            ImGui.SameLine();
            if (Button("Admit as a new player", favoured == AdmissionAction.Admit))
            {
                _coordinator.Admit(request.PeerCode);
            }
        }
        else if (Button("Admit", favoured == AdmissionAction.Admit))
        {
            _coordinator.Admit(request.PeerCode);
        }

        ImGui.SameLine();
        if (Button("Deny", favoured == AdmissionAction.Deny))
        {
            _coordinator.Deny(request.PeerCode);
        }
    }

    /// <summary>A secondary button, or the primary one with default focus when it is the favoured answer.</summary>
    private static bool Button(string label, bool primary)
    {
        if (!primary)
        {
            return ActionRow.Secondary(label);
        }

        var pressed = ActionRow.Primary(label);
        ImGui.SetItemDefaultFocus();
        return pressed;
    }
}
