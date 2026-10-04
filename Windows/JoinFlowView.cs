using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The joiner's side of the session window: status, banners, leaving, and the request form.</summary>
internal sealed class JoinFlowView
{
    private readonly SessionCoordinator _coordinator;

    private readonly UiFonts _fonts;

    private readonly SessionEndingView _ending;

    private readonly JoinRequestForm _requestForm;

    public JoinFlowView(
        SessionCoordinator coordinator,
        UiFonts fonts,
        Func<DisplayName> displayName,
        Func<RelinkMemory> relink,
        KeepOrLose keepOrLose)
    {
        _coordinator = coordinator;
        _fonts = fonts;
        _requestForm = new JoinRequestForm(coordinator, displayName, relink);
        _ending = new SessionEndingView(coordinator, fonts, keepOrLose);
    }

    /// <summary>True while a join is under way or admitted, so the session window shows this side.</summary>
    public bool IsActive => _coordinator.Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;

    /// <summary>The status line, countdowns and problems; drawn above the stream.</summary>
    public void DrawStatus()
    {
        var join = _coordinator.Join;

        using (_fonts.Meta.Push())
        {
            ImGui.TextColored(Palette.TextMuted, $"Joining: {DescribeJoin(join.Phase)}");
        }

        if (join.Phase == JoinPhase.AwaitingDecision)
        {
            Banner.Draw(_fonts, BannerKind.Info, $"The DM has {join.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss} left to answer");
        }

        DrawProblems();
        _ending.DrawLeaving(join);
    }

    /// <summary>The join form, for the Join side of the empty state. Its problems are drawn by DrawProblems.</summary>
    public void DrawForm()
    {
        if (!_coordinator.InAHostedSession && (_coordinator.Join.MayRequestAgain || _coordinator.Join.Phase == JoinPhase.Denied))
        {
            if (_coordinator.Join.Phase is JoinPhase.Denied or JoinPhase.Lapsed)
            {
                using var meta = _fonts.Meta.Push();
                ImGui.TextColored(Palette.TextMuted, $"Joining: {DescribeJoin(_coordinator.Join.Phase)}");
            }

            _requestForm.Draw();
        }
    }

    /// <summary>The keep-or-lose offer after leaving, drawn as a card where the stream was.</summary>
    public bool DrawOffer() => _ending.DrawOffer();

    /// <summary>A failed join and undelivered messages, as banners. Never drawn inside a table cell.</summary>
    public void DrawProblems()
    {
        var join = _coordinator.Join;

        if (join.Failure != SessionFailure.None)
        {
            Banner.Draw(_fonts, BannerKind.Danger, SessionFailureMessage.For(join.Failure));
        }

        if (_coordinator.Membership.Undelivered > 0)
        {
            Banner.Draw(
                _fonts, BannerKind.Warning, $"{_coordinator.Membership.Undelivered} messages you sent were not delivered.");
        }
    }

    private static string DescribeJoin(JoinPhase phase) => phase switch
    {
        JoinPhase.Idle => "not in a session",
        JoinPhase.Contacting => "contacting the relay",
        JoinPhase.AwaitingDecision => "waiting for the DM to decide",
        JoinPhase.Admitted => "in the session",
        JoinPhase.Denied => "not admitted",
        JoinPhase.Lapsed => "the DM did not answer in time - you can ask again",
        _ => "stopped after a problem",
    };
}
