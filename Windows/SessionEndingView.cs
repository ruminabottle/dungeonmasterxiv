using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>Shows a joiner the closing countdown, a Leave button, and after leaving the offer to keep the log.</summary>
internal sealed class SessionEndingView
{
    private readonly SessionCoordinator _coordinator;

    private readonly UiFonts _fonts;

    private readonly KeepOrLose _keepOrLose;

    private readonly DangerAction _leave = new();

    private SessionLogOffer? _offer;

    public SessionEndingView(SessionCoordinator coordinator, UiFonts fonts, KeepOrLose keepOrLose)
    {
        _coordinator = coordinator;
        _fonts = fonts;
        _keepOrLose = keepOrLose;
    }

    public void DrawLeaving(JoinAttempt join)
    {
        ArgumentNullException.ThrowIfNull(join);

        if (_coordinator.Membership.Closing is { } closing)
        {
            Banner.Draw(
                _fonts,
                BannerKind.Warning,
                $"The DM has ended this session. It closes in {closing.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss}");
        }

        if (join.Phase != JoinPhase.Admitted)
        {
            return;
        }

        if (_leave.Draw("Leave session", "Yes, leave"))
        {
            _offer = _keepOrLose.Open();
            _coordinator.Membership.Leave();
        }
    }

    /// <summary>Draws the offer while it is open; returns true when it drew one.</summary>
    public bool DrawOffer()
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

        using var card = Card.Begin(Palette.SurfaceRaised, Palette.Gold);
        ImGui.TextWrapped(
            offer.HasAnything
                ? $"Keep this session's log? {offer.LineCount} lines, {offer.Participants.Count} people. {remaining:mm\\:ss}"
                : $"This session recorded nothing to keep. {remaining:mm\\:ss}");

        if (ActionRow.Primary("Keep"))
        {
            SessionExport.Produce(offer, _keepOrLose.Export);
        }

        ImGui.SameLine();
        if (ActionRow.Secondary("Discard"))
        {
            offer.Decline();
        }

        return true;
    }
}
