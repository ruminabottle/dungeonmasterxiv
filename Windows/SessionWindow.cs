using System;
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
            ImGui.TextUnformatted($"Session code: {code.ToDisplayString()}");
            ImGui.TextWrapped(CodeDisclosure);
            ImGui.TextUnformatted($"Players admitted: {_coordinator.Audience.Count}");

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

        if (join.Phase is JoinPhase.Idle or JoinPhase.Denied or JoinPhase.Failed)
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

        if (_coordinator.PendingRequestCode is { } requester)
        {
            ImGui.Separator();
            ImGui.TextUnformatted($"Join request from {requester}");
            ImGui.TextWrapped(AdmissionDisclosure);

            if (ImGui.Button("Admit"))
            {
                _coordinator.Admit(requester);
            }

            ImGui.SameLine();
            if (ImGui.Button("Deny"))
            {
                _coordinator.Deny(requester);
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
        _ => "stopped after a problem",
    };
}
