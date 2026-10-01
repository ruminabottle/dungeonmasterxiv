using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

internal sealed class SessionEndingView
{
    private readonly SessionCoordinator _coordinator;

    private readonly KeepOrLose _keepOrLose;

    private SessionLogOffer? _offer;

    public SessionEndingView(SessionCoordinator coordinator, KeepOrLose keepOrLose)
    {
        _coordinator = coordinator;
        _keepOrLose = keepOrLose;
    }

    public void Draw(JoinAttempt join)
    {
        ArgumentNullException.ThrowIfNull(join);

        if (_coordinator.Membership.Closing is { } closing)
        {
            ImGui.TextUnformatted(
                $"The DM has ended this session. It closes in {closing.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss}");
        }

        if (DrawTheOffer())
        {
            return;
        }

        if (join.Phase != JoinPhase.Admitted)
        {
            return;
        }

        if (ImGui.Button("Leave session"))
        {
            _offer = _keepOrLose.Open();
            _coordinator.Membership.Leave();
        }
    }

    private bool DrawTheOffer()
    {
        if (_offer is not { IsOpen: true } offer)
        {
            return false;
        }

        var remaining = offer.RemainingAt(DateTimeOffset.UtcNow.UtcTicks);
        if (offer.ElapseTo(DateTimeOffset.UtcNow.UtcTicks))
        {
            return false;
        }

        ImGui.TextUnformatted(
            offer.HasAnything
                ? $"Keep this session's log? {offer.LineCount} lines, {offer.Participants.Count} people. {remaining:mm\\:ss}"
                : $"This session recorded nothing to keep. {remaining:mm\\:ss}");

        if (ImGui.Button("Keep"))
        {
            SessionExport.Produce(offer, _keepOrLose.Export);
        }

        if (ImGui.Button("Discard"))
        {
            offer.Decline();
        }

        return true;
    }
}
