using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>The session window: hosting controls and status, the join flow, admission prompts and chat.</summary>
public sealed class SessionWindow : Window
{
    private const string CodeDisclosure =
        "Your session code is not a secret. Anyone who has it can ask to join — you decide who gets in.";

    private const string CodeChangedWarning =
        "Your session code changed while you were disconnected, because it was taken by another "
        + "session. Your players are still holding the old one - read them the new code below.";

    private readonly SessionCoordinator _coordinator;

    private readonly HostingCampaign _hosting;

    private readonly HostCampaignPicker _campaignPicker;

    private readonly AdmissionPromptView _admissionPrompts;

    private readonly JoinFlowView _joinFlow;

    private readonly MessageComposeView _compose;

    public SessionWindow(
        SessionCoordinator coordinator,
        Func<DisplayName> displayName,
        HostingCampaign hosting,
        Func<RelinkMemory> relink,
        KeepOrLose keepOrLose)
        : base("Dungeon Master XIV session###dmx-session")
    {
        _coordinator = coordinator;
        _admissionPrompts = new AdmissionPromptView(coordinator);
        _hosting = hosting;
        _campaignPicker = new HostCampaignPicker(hosting);
        _joinFlow = new JoinFlowView(coordinator, displayName, relink, keepOrLose);
        _compose = new MessageComposeView(coordinator);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(420, 260),
            MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Open() => IsOpen = true;

    public override void Draw()
    {
        DrawHosting();
        ImGui.Separator();
        _joinFlow.Draw();

        _admissionPrompts.Draw();

        _compose.Draw();
    }

    private void DrawHosting()
    {
        var host = _coordinator.Host;
        ImGui.TextUnformatted($"Hosting: {DescribeHosting(host.Phase)}");

        if (host.Phase == HostingPhase.Hosting && host.Code is { } code)
        {
            if (host.CodeChangedMidSession)
            {
                ImGui.TextWrapped(CodeChangedWarning);
                if (ImGui.Button("I have told them"))
                {
                    _coordinator.Host.AcknowledgeCodeChange();
                }
            }

            if (_coordinator.Grace.IsRunning)
            {
                ImGui.TextWrapped(
                    $"Lost contact with the relay. Reconnecting - the session ends in "
                    + $"{_coordinator.Grace.Remaining:mm\\:ss} if it does not come back.");
            }

            ImGui.TextUnformatted($"Session code: {code.ToDisplayString()}");

            ImGui.SameLine();
            if (ImGui.Button("Copy"))
            {
                ImGui.SetClipboardText(code.ToClipboardString());
            }

            ImGui.TextWrapped(CodeDisclosure);

            var audience = _coordinator.Audience;
            ImGui.TextUnformatted($"Players admitted: {audience.Count}");

            RosterView.Draw(audience.Recipients.Select(peer => (peer.DisplayName.Value, peer.Role)));

            if (ImGui.Button("End session"))
            {
                _coordinator.StopHosting(DateTimeOffset.UtcNow);

                _hosting.Ended();
            }

            return;
        }

        if (host.Failure != SessionFailure.None)
        {
            ImGui.TextWrapped(SessionFailureMessage.For(host.Failure));
        }

        if (!InAJoinedSession() && !InAHostedSession())
        {
            _campaignPicker.Draw();

            if (ImGui.Button("Start session"))
            {
                _hosting.StartFor();
                _coordinator.StartHosting();
            }
        }
    }

    private bool InAJoinedSession() => _coordinator.InAJoinedSession;

    private bool InAHostedSession() => _coordinator.InAHostedSession;

    private static string DescribeHosting(HostingPhase phase) => phase switch
    {
        HostingPhase.NotHosting => "not hosting",
        HostingPhase.Registering => "registering with the relay",
        HostingPhase.Hosting => "live",
        _ => "stopped after a problem",
    };

}
