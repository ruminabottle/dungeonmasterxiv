using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
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
            ImGui.PushID(request.PeerCode.Value);

            ImGui.TextWrapped(AdmissionPrompt.Headline(request));
            using (_fonts.Meta.Push())
            {
                ImGui.TextColored(Palette.TextMuted, $"This request lapses in {request.RemainingAt(now):mm\\:ss}");
            }

            DrawActions(request);
            ImGui.PopID();
        }
    }

    private void DrawActions(PendingAdmission request)
    {
        var favoured = AdmissionPrompt.Favoured(request) == AdmissionAction.Admit;

        if (_coordinator.CanAdmitAsClaimed(request))
        {
            var label = string.IsNullOrEmpty(request.RelinkLabel) ? "returning player" : request.RelinkLabel;
            if (ActionRow.Primary($"Admit as {label}"))
            {
                _coordinator.Admit(request.PeerCode, asClaimed: true);
            }

            ImGui.SameLine();
            if (ActionRow.Secondary("Admit as a new player"))
            {
                _coordinator.Admit(request.PeerCode);
            }
        }
        else if (ActionRow.Primary("Admit"))
        {
            _coordinator.Admit(request.PeerCode);
        }

        if (favoured)
        {
            ImGui.SetItemDefaultFocus();
        }

        ImGui.SameLine();
        if (ActionRow.Secondary("Deny"))
        {
            _coordinator.Deny(request.PeerCode);
        }
    }
}
