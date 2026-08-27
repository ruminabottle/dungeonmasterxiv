using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// Hosting and joining. Draws the state the session layer is in and raises the intents that change
/// it; it decides nothing itself.
/// </summary>
public sealed class SessionWindow : Window
{
    // R-1.7a, verbatim. A PR may not substitute its own wording for these.
    private const string CodeDisclosure =
        "Your session code is not a secret. Anyone who has it can ask to join — you decide who gets in.";

    private const string AdmissionDisclosure =
        "This request shows a code, not a character name. Only admit people you arranged to play with.";

    // Not R-1.7a copy — R-1.7a covers the session window, the admission prompt and settings, and does
    // not supply wording for these. Written here under the same constraint: no phrasing from its
    // forbidden list, and no claim that a session is protected when nobody checked.
    private const string CompareOutOfBand =
        "Ask the joining player to read their code back to you over voice or chat, and confirm it "
        + "matches. Do not ask them for it through the plugin - a channel someone has tampered with "
        + "cannot prove it has not been tampered with.";

    private const string UnverifiedWarning =
        "Admitted without the code being compared. This session is not protected against someone "
        + "sitting in the middle of it.";

    private const string CodeChangedWarning =
        "Your session code changed while you were disconnected, because it was taken by another "
        + "session. Your players are still holding the old one - read them the new code below.";

    private readonly SessionCoordinator _coordinator;
    private string _codeEntry = string.Empty;

    /// <param name="coordinator">The session layer this window reflects.</param>
    public SessionWindow(SessionCoordinator coordinator)
        : base("Dungeon Master XIV session###dmx-session")
    {
        _coordinator = coordinator;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(420, 260),
            MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue),
        };
    }

    /// <summary>Opens this window.</summary>
    public void Open() => IsOpen = true;

    /// <inheritdoc />
    public override void Draw()
    {
        DrawHosting();
        ImGui.Separator();
        DrawJoining();
    }

    private void DrawHosting()
    {
        var host = _coordinator.Host;
        ImGui.TextUnformatted($"Hosting: {DescribeHosting(host.Phase)}");

        if (host.Phase == HostingPhase.Hosting && host.Code is { } code)
        {
            if (host.CodeChangedMidSession)
            {
                ImGui.TextWrapped(CodeChangedWarning);
                if (ImGui.Button("I have told them"))
                {
                    _coordinator.Host.AcknowledgeCodeChange();
                }
            }

            if (_coordinator.Grace.IsRunning)
            {
                // R-1.4: state is held but visibly not live, with the wait bounded on screen.
                ImGui.TextWrapped(
                    $"Lost contact with the relay. Reconnecting - the session ends in "
                    + $"{_coordinator.Grace.Remaining:mm\\:ss} if it does not come back.");
            }

            ImGui.TextUnformatted($"Session code: {code.ToDisplayString()}");
            ImGui.TextWrapped(CodeDisclosure);

            var audience = _coordinator.Audience;
            ImGui.TextUnformatted($"Players admitted: {audience.Count}");
            if (audience.Count > audience.ConfirmedCount)
            {
                ImGui.TextWrapped(
                    $"{audience.Count - audience.ConfirmedCount} admitted without the code being compared.");
            }

            if (ImGui.Button("End session"))
            {
                _coordinator.StopHosting();
            }

            return;
        }

        if (host.Failure != SessionFailure.None)
        {
            ImGui.TextWrapped(SessionFailureMessage.For(host.Failure));
        }

        if (ImGui.Button("Start session"))
        {
            _coordinator.StartHosting();
        }
    }

    private void DrawJoining()
    {
        var join = _coordinator.Join;
        ImGui.TextUnformatted($"Joining: {DescribeJoin(join.Phase)}");

        if (join.Phase == JoinPhase.AwaitingDecision)
        {
            // R-1.3c's harder half: the bound is visible while the wait runs, not only at the end.
            ImGui.TextUnformatted($"The DM has {join.RemainingAt(DateTimeOffset.UtcNow):mm\\:ss} left to answer");
        }

        if (join.MayRequestAgain || join.Phase == JoinPhase.Denied)
        {
            ImGui.InputText("Session code", ref _codeEntry, 16);
            if (ImGui.Button("Request to join") && SessionCode.TryParse(_codeEntry, out var code))
            {
                _coordinator.RequestJoin(code);
            }
        }

        if (join.Failure != SessionFailure.None)
        {
            ImGui.TextWrapped(SessionFailureMessage.For(join.Failure));
        }

        // Every pending request gets its own prompt. One slot would strand all but the newest, and
        // the stranded players would see a DM who appears to be ignoring them.
        var pending = _coordinator.Admissions.Pending;
        if (pending.Count == 0)
        {
            return;
        }

        ImGui.Separator();
        ImGui.TextWrapped(AdmissionDisclosure);

        var now = DateTimeOffset.UtcNow;

        // Copied because Admit and Deny mutate the pending list, and this is a draw callback.
        foreach (var request in pending.ToArray())
        {
            ImGui.Separator();
            ImGui.TextUnformatted(request.IsRelink
                ? $"Relink request from {request.PeerCode}"
                : $"Join request from {request.PeerCode}");

            ImGui.TextUnformatted($"Code to compare: {request.Fingerprint}");
            ImGui.TextWrapped(CompareOutOfBand);

            // R-1.3c: the wait is visibly bounded WHILE it runs, not only when it ends.
            var remaining = request.RemainingAt(now);
            ImGui.TextUnformatted($"This request lapses in {remaining:mm\\:ss}");

            // R-1.3a: a deliberate act, never a pre-ticked box. Starts false every time.
            var confirmed = request.FingerprintConfirmed;
            if (ImGui.Checkbox($"The code matched what they read to me##{request.PeerCode}", ref confirmed)
                && confirmed)
            {
                request.ConfirmFingerprintMatched();
            }

            if (!request.FingerprintConfirmed)
            {
                ImGui.TextWrapped(UnverifiedWarning);
            }

            if (ImGui.Button($"Admit##{request.PeerCode}"))
            {
                _coordinator.Admit(request.PeerCode);
            }

            ImGui.SameLine();
            if (ImGui.Button($"Deny##{request.PeerCode}"))
            {
                _coordinator.Deny(request.PeerCode);
            }
        }
    }

    // Every phase gets a sentence. R-1.3 forbids leaving anyone looking at an ambiguous spinner,
    // so there is no state here that renders as "..." and nothing else.
    private static string DescribeHosting(HostingPhase phase) => phase switch
    {
        HostingPhase.NotHosting => "not hosting",
        HostingPhase.Registering => "registering with the relay",
        HostingPhase.Hosting => "live",
        _ => "stopped after a problem",
    };

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
