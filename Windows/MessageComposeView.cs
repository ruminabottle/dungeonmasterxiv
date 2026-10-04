using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Rolls;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The chat box: sends a message or shares a roll in a session, or rolls only for you outside one.</summary>
internal sealed class MessageComposeView
{
    private readonly SessionCoordinator _coordinator;

    private readonly RollEvaluator _rolls = new(new SystemDieRoller());

    private readonly List<LocalRoll> _local = new();

    private string _entry = string.Empty;

    private string? _refusal;

    private bool _refocus;

    public MessageComposeView(SessionCoordinator coordinator) => _coordinator = coordinator;

    internal string? Refusal => _refusal;

    public IReadOnlyList<LocalRoll> LocalRolls => _local;

    /// <summary>The height Draw needs, so the stream above can take the rest.</summary>
    public float Height =>
        ImGui.GetFrameHeightWithSpacing() + (_refusal is null ? 0f : ImGui.GetTextLineHeightWithSpacing() * 2f);

    public void Draw()
    {
        if (_coordinator.InASession && _local.Count > 0)
        {
            _local.Clear();
        }

        var reconnecting = _coordinator.ReconnectingLine is not null;

        using (ImRaii.Disabled(reconnecting))
        {
            var send = ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize("Send").X + (ImGui.GetStyle().FramePadding.X * 2f);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - send);

            if (_refocus)
            {
                ImGui.SetKeyboardFocusHere();
                _refocus = false;
            }

            var entered = ImGui.InputTextWithHint(
                "##compose",
                "Say something, or /roll 1d20",
                ref _entry,
                MessageLimits.Default.MaxUtf8Bytes,
                ImGuiInputTextFlags.EnterReturnsTrue);

            ImGui.SameLine();
            if (ActionRow.Primary("Send") || entered)
            {
                Submit();
                _refocus = true;
            }
        }

        if (_refusal is { } refusal)
        {
            Banner.Refusal(refusal);
        }
    }

    internal void Submit()
    {
        if (RollCommand.TryRead(_entry, out var expression))
        {
            Roll(expression);
            return;
        }

        var draft = _coordinator.Say(_entry, DateTimeOffset.UtcNow);

        _refusal = draft.IsAccepted ? null : draft.Reason;

        if (draft.IsAccepted)
        {
            _entry = string.Empty;
        }
    }

    private void Roll(string expression)
    {
        var outcome = _rolls.Evaluate(expression);
        if (!outcome.Evaluated)
        {
            _refusal = outcome.Message;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var roll = SharedRoll.From(expression, outcome);

        if (_coordinator.InASession)
        {
            _refusal = _coordinator.ShareRoll(roll, now);
        }
        else
        {
            _local.Add(new LocalRoll(now.UtcTicks, roll));
            _refusal = null;
        }

        if (_refusal is null)
        {
            _entry = string.Empty;
        }
    }
}
