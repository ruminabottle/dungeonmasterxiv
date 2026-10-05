using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The panel's Chat tab: the stream and the message box, with the notices a person mid-conversation needs.</summary>
internal sealed class ChatTab
{
    private readonly SessionCoordinator _coordinator;

    private readonly UiFonts _fonts;

    private readonly Func<DisplayName> _displayName;

    private readonly JoinFlowView _joinFlow;

    private readonly Action _showSession;

    private readonly MessageComposeView _compose;

    private readonly StreamView _stream;

    public ChatTab(
        SessionCoordinator coordinator,
        UiFonts fonts,
        Func<DisplayName> displayName,
        JoinFlowView joinFlow,
        Action showSession)
    {
        _coordinator = coordinator;
        _fonts = fonts;
        _displayName = displayName;
        _joinFlow = joinFlow;
        _showSession = showSession;
        _compose = new MessageComposeView(coordinator);
        _stream = new StreamView(coordinator, fonts);
    }

    public void Draw()
    {
        if (_coordinator.ReconnectingLine is { } reconnecting)
        {
            Banner.Draw(_fonts, BannerKind.Warning, reconnecting);
        }

        DrawJoinRequestNotice();

        var you = new SpeakerName(
            _displayName().Value, _coordinator.InAHostedSession ? SessionRole.DungeonMaster : SessionRole.Player);
        var streamHeight = ImGui.GetContentRegionAvail().Y - _compose.Height - ImGui.GetStyle().ItemSpacing.Y;
        _stream.Draw(Math.Max(streamHeight, ImGui.GetFrameHeight()), you, _compose.LocalRolls, _joinFlow.DrawOffer);
        _compose.Draw();
    }

    /// <summary>Tells the host someone is waiting. Drawn locally and never recorded or sent (rolls R-2.3).</summary>
    private void DrawJoinRequestNotice()
    {
        var pending = _coordinator.Admissions.Pending;
        if (!_coordinator.InAHostedSession || pending.Count == 0)
        {
            return;
        }

        var text = pending.Count == 1
            ? $"{pending[0].DisplayName.Value} is asking to join"
            : $"{pending.Count} people are asking to join";

        if (Banner.Draw(_fonts, BannerKind.Info, text, "Review"))
        {
            _showSession();
        }
    }
}
