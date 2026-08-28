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

    // The joiner's side of CompareOutOfBand. Same instruction, same constraint, addressed to the
    // person who until now was told to read out a code their client never showed them (BUG-31).
    private const string ReadYourCodeAloud =
        "Read this code to your DM over voice or chat while they decide, and check it matches what "
        + "they see. Do not send it through the plugin - a channel someone has tampered with cannot "
        + "prove it has not been tampered with.";

    // R-1.3a-i: the honest rendering when there is nothing to compare. Never a blank space where a
    // code would go, and never a placeholder that could be mistaken for one.
    private const string NoCodeToCompare =
        "Your DM's client has not sent a code to compare. You cannot check who you are talking to, "
        + "and being admitted will not tell you.";

    private const string AdmittedUncompared =
        "You were admitted without ever having a code to compare. Nothing here proves the DM is who "
        + "you think - it only proves someone admitted you.";

    private const string CodeChangedWarning =
        "Your session code changed while you were disconnected, because it was taken by another "
        + "session. Your players are still holding the old one - read them the new code below.";

    private readonly SessionCoordinator _coordinator;

    /// <summary>
    /// What to call ourselves when asking to join (R-1.3e). A function rather than a value because
    /// the answer changes with who is logged in, and a name captured once is stale exactly when a
    /// player switches character — see <c>LocalCharacterName</c>.
    /// </summary>
    private readonly Func<DisplayName> _displayName;

    private string _codeEntry = string.Empty;

    /// <param name="coordinator">The session layer this window reflects.</param>
    /// <param name="displayName">What to call ourselves when joining (R-1.3e). Asked each time.</param>
    public SessionWindow(SessionCoordinator coordinator, Func<DisplayName> displayName)
        : base("Dungeon Master XIV session###dmx-session")
    {
        _coordinator = coordinator;
        _displayName = displayName;
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
                ImGui.SetClipboardText(code.ToDisplayString());
            }

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

            // R-1.3a-i: the joiner's half of the comparison, and it must be here rather than after
            // admission. The DM is shown the same value in their prompt; whichever side reads it
            // out, the other confirms.
            if (join.Fingerprint is { } fingerprint)
            {
                ImGui.TextUnformatted($"Code to compare: {fingerprint}");
                ImGui.TextWrapped(ReadYourCodeAloud);
            }
            else
            {
                ImGui.TextWrapped(NoCodeToCompare);
            }
        }

        // Said once the decision is made, because by then the code on screen is no longer something
        // that could have informed it. Reads from the snapshot taken at admission, not from whether
        // a fingerprint exists now - the host's key arrives again in the acceptance envelope, so
        // "do we have one?" is true a moment later and answers the wrong question (A-1.3f-1).
        if (join.Phase == JoinPhase.Admitted && !join.FingerprintWasComparableAtDecision)
        {
            ImGui.TextWrapped(AdmittedUncompared);
        }

        if (join.MayRequestAgain || join.Phase == JoinPhase.Denied)
        {
            ImGui.InputText("Session code", ref _codeEntry, 16);
            if (ImGui.Button("Request to join") && SessionCode.TryParse(_codeEntry, out var code))
            {
                // R-1.3e: we name ourselves on the request, so the DM's prompt has a name without a
                // second round trip. It is a label and never a credential — the fingerprint the DM
                // compares is what decides, and it is unaffected by whatever this returns.
                _coordinator.RequestJoin(code, _displayName());
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
            // The ONLY thing a resolved relink changes. Everything below this line — the
            // fingerprint, the out-of-band warning, the deliberate confirmation, the two buttons —
            // is identical for a relink and a first-time join, and must stay identical. R-1.5:
            // the DM approves every relink, every session, and a match must not shorten the path.
            ImGui.TextUnformatted(AdmissionPrompt.Headline(request));

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

            // The prompt starts with NEITHER answer selected, for a relink exactly as for a first
            // join. Pre-selecting Accept for a recognised returning player would be the helpful
            // thing and it is forbidden: a DM pressing Enter on a focused button leaves the same
            // record as a DM who compared the fingerprint, which makes the record false rather than
            // merely weaker (R-1.5, R-1.3a). The favoured action is decided in Core so a change of
            // mind has to happen where a test is watching.
            if (ImGui.Button($"Admit##{request.PeerCode}"))
            {
                _coordinator.Admit(request.PeerCode);
            }

            if (AdmissionPrompt.Favoured(request) == AdmissionAction.Admit)
            {
                ImGui.SetItemDefaultFocus();
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
