using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The session window: how to host or join, the session's status and people, requests, the stream and the chat box.</summary>
internal sealed class SessionWindow : ThemedWindow
{
    private const string CodeChangedWarning =
        "Your session code changed while you were disconnected, because it was taken by another "
        + "session. Your players are still holding the old one - read them the new code below.";

    private readonly SessionCoordinator _coordinator;

    private readonly Func<DisplayName> _displayName;

    private readonly HostingCampaign _hosting;

    private readonly HostCampaignPicker _campaignPicker;

    private readonly AdmissionPromptView _admissionPrompts;

    private readonly JoinFlowView _joinFlow;

    private readonly MessageComposeView _compose;

    private readonly StreamView _stream;

    private readonly DangerAction _endSession = new();

    /// <summary>Below this width the Host and Join paths stack instead of sitting side by side.</summary>
    private const float SideBySideWidth = 560f;

    public SessionWindow(
        SessionCoordinator coordinator,
        UiFonts fonts,
        IPluginLog log,
        Func<DisplayName> displayName,
        HostingCampaign hosting,
        Func<RelinkMemory> relink,
        KeepOrLose keepOrLose)
        : base("Session###dmx-session", fonts, log)
    {
        _coordinator = coordinator;
        _displayName = displayName;
        _hosting = hosting;
        _admissionPrompts = new AdmissionPromptView(coordinator, fonts);
        _campaignPicker = new HostCampaignPicker(hosting);
        _joinFlow = new JoinFlowView(coordinator, fonts, displayName, relink, keepOrLose);
        _compose = new MessageComposeView(coordinator);
        _stream = new StreamView(coordinator, fonts);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Open() => IsOpen = true;

    public override void PreDraw()
    {
        WindowName = $"{Title()}###dmx-session";
        base.PreDraw();
    }

    protected override void DrawContent()
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

        var you = new SpeakerName(_displayName().Value, _coordinator.InAHostedSession ? SessionRole.DungeonMaster : SessionRole.Player);
        var streamHeight = ImGui.GetContentRegionAvail().Y - _compose.Height - ImGui.GetStyle().ItemSpacing.Y;
        _stream.Draw(Math.Max(streamHeight, ImGui.GetFrameHeight()), you, _compose.LocalRolls, _joinFlow.DrawOffer);
        _compose.Draw();
    }

    private string Title() =>
        _coordinator.InAHostedSession && _hosting.Current is { } campaign ? CampaignName.For(campaign) : "Session";

    private void DrawNotInASession()
    {
        if (_joinFlow.OfferIsOpen)
        {
            return;
        }

        if (_coordinator.Host.Failure != SessionFailure.None)
        {
            Banner.Draw(Fonts, BannerKind.Danger, SessionFailureMessage.For(_coordinator.Host.Failure));
        }

        EmptyState.Draw(Fonts, "No session yet", "Start one as the DM, or join one with the code your DM gives you.");

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
        Section.Heading(Fonts, "Host");
        _campaignPicker.Draw();
        if (ActionRow.Primary("Start session"))
        {
            _hosting.StartFor();
            _coordinator.StartHosting();
        }
    }

    private void DrawJoinPath()
    {
        Section.Heading(Fonts, "Join");
        _joinFlow.DrawForm();
    }

    private void DrawHosting()
    {
        var host = _coordinator.Host;

        if (host.Phase == HostingPhase.Registering)
        {
            Banner.Draw(Fonts, BannerKind.Info, "Hosting: registering with the relay");
            return;
        }

        if (host.CodeChangedMidSession)
        {
            Banner.Draw(Fonts, BannerKind.Warning, CodeChangedWarning);
            if (ActionRow.Secondary("I have told them"))
            {
                host.AcknowledgeCodeChange();
            }
        }

        if (_coordinator.ReconnectingLine is { } reconnecting)
        {
            Banner.Draw(Fonts, BannerKind.Warning, reconnecting);
        }

        if (host.Code is { } code)
        {
            CodeDisplay.Draw(Fonts, code);
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
        if (!Section.Collapsible(Fonts, $"At the table · {roster.Count}###table"))
        {
            return;
        }

        foreach (var entry in roster)
        {
            var away = host
                && PeerCode.TryParse(entry.PeerCode, out var peer)
                && _coordinator.Drops.WhenDropped(peer) is not null;
            RosterRow.Draw(Fonts, new SpeakerName(DisplayName.OrNone(entry.DisplayName).Value, entry.Role), away);
        }

        if (!host)
        {
            return;
        }

        using (Fonts.Meta.Push())
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
