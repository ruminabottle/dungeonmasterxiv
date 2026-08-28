using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Campaigns;
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


    // Not R-1.7a copy — R-1.7a covers the session window, the admission prompt and settings, and does
    // not supply wording for these. Written here under the same constraint: no phrasing from its
    // forbidden list, and no claim that a session is protected when nobody checked.


    private const string CodeChangedWarning =
        "Your session code changed while you were disconnected, because it was taken by another "
        + "session. Your players are still holding the old one - read them the new code below.";

    private readonly SessionCoordinator _coordinator;

    /// <summary>Which campaign the hosted session belongs to (A-1.9i). Told when hosting starts and ends.</summary>
    private readonly HostingCampaign _hosting;

    /// <summary>The resume offer beside the host button. Its own surface; see <see cref="HostCampaignPicker"/>.</summary>
    private readonly HostCampaignPicker _campaignPicker;

    /// <summary>The DM's pending-request prompts. Its own surface; see <see cref="AdmissionPromptView"/>.</summary>
    private readonly AdmissionPromptView _admissionPrompts;

    /// <summary>The joiner's surface. Its own type; see <see cref="JoinFlowView"/>.</summary>
    private readonly JoinFlowView _joinFlow;

    /// <param name="coordinator">The session layer this window reflects.</param>
    /// <param name="displayName">What to call ourselves when joining (R-1.3e). Asked each time.</param>
    /// <param name="hosting">
    /// Which campaign a hosted session belongs to (A-1.9i). Settled when hosting starts, forgotten
    /// when it ends, and never asked about first — hosting is one action.
    /// </param>
    public SessionWindow(SessionCoordinator coordinator, Func<DisplayName> displayName, HostingCampaign hosting)
        : base("Dungeon Master XIV session###dmx-session")
    {
        _coordinator = coordinator;
        _admissionPrompts = new AdmissionPromptView(coordinator);
        _hosting = hosting;
        _campaignPicker = new HostCampaignPicker(hosting);
        _joinFlow = new JoinFlowView(coordinator, displayName);
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
        _joinFlow.Draw();

        // Drawn last and from here rather than from inside the join flow, where it used to live:
        // these are the DM's prompts, and a joiner's surface was never their place. Composed rather
        // than called through, so the three surfaces can be read and changed independently.
        _admissionPrompts.Draw();
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

            // R-1.3i. The clipboard is an OS facility, not game input (D-1) and not a network
            // destination (D-2) — R-1.3i records that explicitly so a reviewer meeting this call
            // does not have to reason it out.
            //
            // It copies ToDisplayString(), the SAME expression the line above renders, so what the
            // DM copies is what the DM is looking at. That is also what makes it safe to paste:
            // A-1.18 requires the copied value to be accepted verbatim by the join field, and the
            // grouped form is accepted because SessionCode.TryParse strips hyphens.
            // ADisplayedCodeParsesBackToTheSameCode is what holds that, and it fails if either half
            // moves — if Group switched to spaces, the paste would break and that test would go red.
            ImGui.SameLine();
            if (ImGui.Button("Copy"))
            {
                ImGui.SetClipboardText(code.ToClipboardString());
            }

            ImGui.TextWrapped(CodeDisclosure);

            var audience = _coordinator.Audience;
            ImGui.TextUnformatted($"Players admitted: {audience.Count}");

            // R-1.3f / A-1.13: the DM sees the display NAMES of everyone currently admitted. The
            // count above is not that -- it says how many without saying who, which is the half the
            // criterion is about. Read from the audience rather than from the broadcast roster,
            // because the host AUTHORS the roster (D-3) and never receives one.
            RosterView.Draw(audience.Recipients.Select(peer => (peer.DisplayName.Value, peer.Role)));

            if (audience.Count > audience.ConfirmedCount)
            {
                ImGui.TextWrapped(
                    $"{audience.Count - audience.ConfirmedCount} admitted without the code being compared.");
            }

            if (ImGui.Button("End session"))
            {
                _coordinator.StopHosting();

                // The ASSOCIATION ends; the campaign does not. R-1.6 keeps participants on the DM's
                // machine, so a session ending must not take them with it.
                _hosting.Ended();
            }

            return;
        }

        if (host.Failure != SessionFailure.None)
        {
            ImGui.TextWrapped(SessionFailureMessage.For(host.Failure));
        }

        // R-1.3h: a client that has joined a session offers NO WAY to host one. The control is
        // ABSENT rather than disabled — the requirement is explicit that a greyed control which
        // still occupies the UI fails, because it invites exactly the question the exclusivity
        // exists to remove.
        if (!InAJoinedSession())
        {
            // A-1.9j: the resume offer sits BEFORE the button and never gates it. Drawn only when
            // there is something to resume, so a first run stays one action.
            _campaignPicker.Draw();

            if (ImGui.Button("Start session"))
            {
                // A-1.9i: settled BEFORE hosting starts and without asking anything, so the session
                // has a campaign from its first frame rather than acquiring one later.
                _hosting.StartFor();
                _coordinator.StartHosting();
            }
        }
    }

    /// <summary>
    /// Whether this client is in a session it joined — for the life of that session (R-1.3h).
    /// </summary>
    /// <remarks>
    /// The phases mirror <c>SessionCoordinator.JoinNeedsConnection</c>, which is the existing
    /// answer to "is this client engaged in a join". <c>Denied</c>, <c>Lapsed</c> and
    /// <c>Failed</c> are deliberately absent: the attempt is over, the client is in no session,
    /// and hosting becomes offerable again.
    /// </remarks>
    /// <remarks>
    /// <b>Asks Core rather than reading a phase (BUG-53).</b> This used to match Contacting,
    /// AwaitingDecision and Admitted, so an admitted joiner whose link dropped was offered
    /// "Start session" while the DM was still holding their seat — R-1.3h violated by a network
    /// hiccup. The phase cannot answer this: four predecessors reach Failed and only one of them
    /// holds a seat. The seat clock is what expires, so Core is what decides.
    /// </remarks>
    private bool InAJoinedSession() => _coordinator.InAJoinedSession;

    private static string DescribeHosting(HostingPhase phase) => phase switch
    {
        HostingPhase.NotHosting => "not hosting",
        HostingPhase.Registering => "registering with the relay",
        HostingPhase.Hosting => "live",
        _ => "stopped after a problem",
    };

}
