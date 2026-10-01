using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

internal sealed class AdmissionPromptView
{
    private const string AdmissionDisclosure =
        "The name shown is chosen by the requester, not proof of who they are - the code is. Only admit "
        + "people you arranged to play with.";
    private const string CompareOutOfBand =
        "Ask the joining player to read their code back to you over voice or chat, and confirm it "
        + "matches. Do not ask them for it through the plugin - a channel someone has tampered with "
        + "cannot prove it has not been tampered with.";
    private const string UnverifiedWarning =
        "Admitted without the code being compared. This session is not protected against someone "
        + "sitting in the middle of it.";

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
        ImGui.TextWrapped(AdmissionDisclosure);

        var now = DateTimeOffset.UtcNow;

        foreach (var request in pending.ToArray())
        {
            ImGui.Separator();
            ImGui.TextUnformatted(AdmissionPrompt.Headline(request));

            ImGui.TextUnformatted($"Code to compare: {request.Fingerprint}");
            ImGui.TextWrapped(CompareOutOfBand);

            var remaining = request.RemainingAt(now);
            ImGui.TextUnformatted($"This request lapses in {remaining:mm\\:ss}");

            if (AdmissionPrompt.ComparabilityNote(request) is { Length: > 0 } note)
            {
                ImGui.TextWrapped(note);
            }

            if (AdmissionPrompt.OffersConfirmation(request))
            {
                var confirmed = request.FingerprintConfirmed;
                if (ImGui.Checkbox($"The code matched what they read to me##{request.PeerCode}", ref confirmed)
                    && confirmed)
                {
                    request.ConfirmFingerprintMatched();
                }
            }

            if (!request.FingerprintConfirmed)
            {
                ImGui.TextWrapped(UnverifiedWarning);
            }

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
