using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Rolls;

namespace DungeonMasterXIV.Windows;

/// <summary>The chat box: sends a message to the session, or rolls dice locally when given a roll command.</summary>
internal sealed class MessageComposeView
{
    private readonly SessionCoordinator _coordinator;

    private readonly RollEvaluator _rolls = new(new SystemDieRoller());

    private string _entry = string.Empty;

    private string? _refusal;

    public MessageComposeView(SessionCoordinator coordinator) => _coordinator = coordinator;

    internal string? Refusal => _refusal;

    public void Draw()
    {
        ImGui.InputText("Say", ref _entry, MessageLimits.Default.MaxUtf8Bytes);

        if (ImGui.Button("Send"))
        {
            Submit();
        }

        if (_refusal is { } refusal)
        {
            ImGui.TextUnformatted(refusal);
        }
    }

    internal void Submit()
    {
        if (RollCommand.TryRead(_entry, out var expression))
        {
            Roll(expression);
            return;
        }

        var draft = _coordinator.Membership.Say(_entry);

        _refusal = draft.IsAccepted ? null : draft.Reason;

        if (draft.IsAccepted)
        {
            _entry = string.Empty;
        }
    }

    private void Roll(string expression)
    {
        var outcome = _rolls.Evaluate(expression);

        _refusal = outcome.Evaluated
            ? outcome.Notice is { } notice ? $"{outcome.Total} — {notice}" : outcome.Total.ToString(CultureInfo.InvariantCulture)
            : outcome.Message;

        if (outcome.Evaluated)
        {
            _entry = string.Empty;
        }
    }
}
