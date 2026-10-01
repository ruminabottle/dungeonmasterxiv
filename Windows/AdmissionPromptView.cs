using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>Shows the host each pending join request with Admit and Deny buttons.</summary>
internal sealed class AdmissionPromptView
{
    private readonly SessionCoordinator _coordinator;

    public AdmissionPromptView(SessionCoordinator coordinator) => _coordinator = coordinator;

    public void Draw()
    {
        var pending = _coordinator.Admissions.Pending;
        if (pending.Count == 0)
        {
            return;
        }

        ImGui.Separator();

        var now = DateTimeOffset.UtcNow;

        foreach (var request in pending.ToArray())
        {
            ImGui.Separator();
            ImGui.TextUnformatted(AdmissionPrompt.Headline(request));

            var remaining = request.RemainingAt(now);
            ImGui.TextUnformatted($"This request lapses in {remaining:mm\\:ss}");

            if (ImGui.Button($"Admit##{request.PeerCode}"))
            {
                _coordinator.Admit(request.PeerCode);
            }

            if (AdmissionPrompt.Favoured(request) == AdmissionAction.Admit)
            {
                ImGui.SetItemDefaultFocus();
            }

            ImGui.SameLine();
            if (ImGui.Button($"Deny##{request.PeerCode}"))
            {
                _coordinator.Deny(request.PeerCode);
            }
        }
    }
}
