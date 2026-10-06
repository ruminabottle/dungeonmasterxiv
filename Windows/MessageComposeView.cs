using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Rolls;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The chat box: sends a message or shares a roll in a session, or rolls only for you outside one.</summary>
internal sealed class MessageComposeView
{
    /// <summary>The most rolls kept outside a session; the oldest goes first.</summary>
    private const int MostLocalRolls = 20;

    private readonly SessionCoordinator _coordinator;

    private readonly AudienceChoice _choice;

    private readonly UiFonts _fonts;

    private readonly RollEvaluator _rolls = new(new SystemDieRoller());

    private readonly List<LocalRoll> _local = new();

    private string _entry = string.Empty;

    private string? _refusal;

    private bool _refocus;

    public MessageComposeView(SessionCoordinator coordinator, AudienceChoice choice, UiFonts fonts)
    {
        _coordinator = coordinator;
        _choice = choice;
        _fonts = fonts;
    }

    internal string? Refusal => _refusal;

    public IReadOnlyList<LocalRoll> LocalRolls => _local;

    /// <summary>The height Draw needs, so the stream above can take the rest.</summary>
    public float Height =>
        (_coordinator.CanAddress ? AudienceRow.Height : 0f)
        + ImGui.GetFrameHeightWithSpacing()
        + (_refusal is null ? 0f : ImGui.GetTextLineHeightWithSpacing() * 2f);

    public void Draw()
    {
        if (_coordinator.InASession && _local.Count > 0)
        {
            _local.Clear();
        }

        _choice.Sync(_coordinator.SessionStarts);

        if (_coordinator.CanAddress)
        {
            AudienceRow.Draw(
                _fonts,
                _choice,
                _coordinator.InAHostedSession,
                _coordinator.CurrentRoster,
                Supports,
                peer => _coordinator.Speakers.For(peer).Name);
        }

        var problem = TargetProblem();

        var reconnecting = _coordinator.ReconnectingLine;

        using (ImRaii.Disabled(reconnecting is not null))
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
                Hint(),
                ref _entry,
                MessageLimits.Default.MaxUtf8Bytes,
                ImGuiInputTextFlags.EnterReturnsTrue);

            if (reconnecting is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(reconnecting);
            }

            ImGui.SameLine();
            bool pressed;
            using (ImRaii.Disabled(problem is not null))
            {
                pressed = ActionRow.Primary("Send");
            }

            if (problem is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(problem);
            }

            if ((pressed || entered) && problem is null)
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
        if (string.IsNullOrWhiteSpace(_entry))
        {
            _refusal = null;
            return;
        }

        if (RollCommand.TryRead(_entry, out var expression))
        {
            Roll(expression);
            return;
        }

        var draft = _coordinator.Say(_entry, DateTimeOffset.UtcNow, Addressed);

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
            _refusal = _coordinator.ShareRoll(roll, now, Addressed);
        }
        else
        {
            _local.Add(new LocalRoll(now.UtcTicks, roll));
            if (_local.Count > MostLocalRolls)
            {
                _local.RemoveAt(0);
            }

            _refusal = null;
        }

        if (_refusal is null)
        {
            _entry = string.Empty;
        }
    }

    private MessageAudience? Addressed => _coordinator.CanAddress ? _choice.Current : null;

    private bool Supports(string peerCode) =>
        PeerCode.TryParse(peerCode, out var peer) && _coordinator.Audience.SupportsAudiences(peer);

    /// <summary>Why the chosen player cannot be messaged now, or null. Never switches the audience on its own.</summary>
    private string? TargetProblem()
    {
        if (!_coordinator.InAHostedSession || _choice.Kind != AudienceKind.Player || _choice.To is not { } to)
        {
            return null;
        }

        var roster = _coordinator.CurrentRoster;
        var name = _coordinator.Speakers.For(to).Name;
        if (roster.All(entry => entry.PeerCode != to))
        {
            return $"{name} is no longer in the session";
        }

        return Supports(to) ? null : AudienceRow.NeedsUpdating(name);
    }

    private string Hint()
    {
        if (!_coordinator.CanAddress)
        {
            return "Say something, or /roll 1d20";
        }

        return _choice.Kind switch
        {
            AudienceKind.DmSide when _coordinator.InAHostedSession => "DM side only: say something, or /roll…",
            AudienceKind.DmSide => "To the DM: say something, or /roll…",
            AudienceKind.Blind => "Blind roll to the DM: /roll 1d20",
            AudienceKind.Player when _choice.To is { } to => $"To {_coordinator.Speakers.For(to).Name}: say something, or /roll…",
            _ => "Say something, or /roll 1d20",
        };
    }
}
