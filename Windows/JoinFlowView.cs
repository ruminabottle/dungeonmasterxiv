using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>Draws the joiner's side of the session window: status, roster, leaving and request form.</summary>
internal sealed class JoinFlowView
{
    private readonly SessionCoordinator _coordinator;

    private readonly SessionEndingView _ending;

    private readonly JoinRequestForm _requestForm;

    public JoinFlowView(
        SessionCoordinator coordinator,
        Func<DisplayName> displayName,
        Func<RelinkMemory> relink,
        KeepOrLose keepOrLose)
    {
        _coordinator = coordinator;
        _requestForm = new JoinRequestForm(coordinator, displayName, relink);
        _ending = new SessionEndingView(coordinator, keepOrLose);
    }

    public void Draw()
    {
        var join = _coordinator.Join;
        ImGui.TextUnformatted($"Joining: {DescribeJoin(join.Phase)}");

        if (join.Phase == JoinPhase.AwaitingDecision)
        {
            ImGui.TextUnformatted($"The DM has {join.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss} left to answer");

        }

        _ending.Draw(join);

        if (join.Phase == JoinPhase.Admitted && _coordinator.Roster.Count > 0)
        {
            ImGui.TextUnformatted(RosterHeading.Text);
            RosterView.Draw(_coordinator.Roster.Select(entry => (entry.DisplayName, entry.Role)));
        }

        if (!InAHostedSession() && (join.MayRequestAgain || join.Phase == JoinPhase.Denied))
        {
            _requestForm.Draw();
        }

        if (join.Failure != SessionFailure.None)
        {
            ImGui.TextWrapped(SessionFailureMessage.For(join.Failure));
        }

        if (_coordinator.Membership.Undelivered > 0)
        {
            ImGui.TextWrapped(
                $"{_coordinator.Membership.Undelivered} messages you sent were not delivered.");
        }

    }

    private bool InAHostedSession() =>
        _coordinator.Host.Phase is HostingPhase.Registering or HostingPhase.Hosting;

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
