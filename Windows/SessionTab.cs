using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The panel's Session tab: how to host or join, the code, who is at the table, and join requests.</summary>
internal sealed class SessionTab
{
    private const string CodeChangedWarning =
        "Your session code changed while you were disconnected, because it was taken by another "
        + "session. Your players are still holding the old one - read them the new code below.";

    private readonly SessionCoordinator _coordinator;

    private readonly UiFonts _fonts;

    private readonly HostingCampaign _hosting;

    private readonly HostCampaignPicker _campaignPicker;

    private readonly AdmissionPromptView _admissionPrompts;

    private readonly JoinFlowView _joinFlow;

    private readonly DangerAction _endSession = new();

    /// <summary>Below this width the Host and Join paths stack instead of sitting side by side.</summary>
    private const float SideBySideWidth = 560f;

    public SessionTab(SessionCoordinator coordinator, UiFonts fonts, HostingCampaign hosting, JoinFlowView joinFlow)
    {
        _coordinator = coordinator;
        _fonts = fonts;
        _hosting = hosting;
        _joinFlow = joinFlow;
        _admissionPrompts = new AdmissionPromptView(coordinator, fonts);
        _campaignPicker = new HostCampaignPicker(hosting);
    }

    public void Draw()
    {
        if (_coordinator.InAHostedSession)
        {
            DrawHosting();
        }
        else if (_joinFlow.IsActive)
        {
            _joinFlow.DrawStatus();
            DrawPeople(host: false);
        }
        else
        {
            DrawNotInASession();
        }

        _admissionPrompts.Draw();
    }

    private void DrawNotInASession()
    {
        if (_joinFlow.OfferIsOpen)
        {
            return;
        }

        if (_coordinator.Host.Failure != SessionFailure.None)
        {
            Banner.Draw(_fonts, BannerKind.Danger, SessionFailureMessage.For(_coordinator.Host.Failure));
        }

        EmptyState.Draw(_fonts, "No session yet", "Start one as the DM, or join one with the code your DM gives you.");

        var sideBySide = ImGui.GetContentRegionAvail().X >= SideBySideWidth * ImGuiHelpers.GlobalScale;
        if (sideBySide)
        {
            DrawPathsSideBySide();
        }
        else
        {
            DrawHostPath();
            DrawJoinPath();
        }

        _joinFlow.DrawProblems();
    }

    private void DrawPathsSideBySide()
    {
        using var table = ImRaii.Table("##paths", 2, ImGuiTableFlags.SizingStretchSame);
        if (!table.Success)
        {
            DrawHostPath();
            DrawJoinPath();
            return;
        }

        ImGui.TableNextColumn();
        DrawHostPath();
        ImGui.TableNextColumn();
        DrawJoinPath();
    }

    private void DrawHostPath()
    {
        Section.Heading(_fonts, "Host");
        _campaignPicker.Draw();
        if (ActionRow.Primary("Start session"))
        {
            _hosting.StartFor();
            _coordinator.StartHosting();
        }
    }

    private void DrawJoinPath()
    {
        Section.Heading(_fonts, "Join");
        _joinFlow.DrawForm();
    }

    private void DrawHosting()
    {
        var host = _coordinator.Host;

        if (host.Phase == HostingPhase.Registering)
        {
            Banner.Draw(_fonts, BannerKind.Info, "Hosting: registering with the relay");
            return;
        }

        if (host.CodeChangedMidSession)
        {
            Banner.Draw(_fonts, BannerKind.Warning, CodeChangedWarning);
            if (ActionRow.Secondary("I have told them"))
            {
                host.AcknowledgeCodeChange();
            }
        }

        if (host.Code is { } code)
        {
            CodeDisplay.Draw(_fonts, code);
        }

        DrawPeople(host: true);

        if (_endSession.Draw("End session", "Yes, end it for everyone"))
        {
            _coordinator.StopHosting(DateTimeOffset.UtcNow);
            _hosting.Ended();
        }
    }

    private void DrawPeople(bool host)
    {
        var roster = _coordinator.CurrentRoster;
        if (!Section.Collapsible(_fonts, $"At the table · {roster.Count}###table"))
        {
            return;
        }

        foreach (var entry in roster)
        {
            var away = host
                && PeerCode.TryParse(entry.PeerCode, out var peer)
                && _coordinator.Drops.WhenDropped(peer) is not null;
            RosterRow.Draw(_fonts, new SpeakerName(DisplayName.OrNone(entry.DisplayName).Value, entry.Role), away);
        }

        if (!host)
        {
            return;
        }

        using (_fonts.Meta.Push())
        {
            ImGui.TextColored(Palette.TextMuted, "Returning players");
        }

        var letIn = _hosting.LetsReturningPlayersIn;
        if (ImGui.RadioButton("Ask me each time", !letIn))
        {
            _hosting.SetReturningPlayers(false);
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Let them straight in", letIn))
        {
            _hosting.SetReturningPlayers(true);
        }
    }
}
