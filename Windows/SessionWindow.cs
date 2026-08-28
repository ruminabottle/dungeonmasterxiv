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


    // Not R-1.7a copy — R-1.7a covers the session window, the admission prompt and settings, and does
    // not supply wording for these. Written here under the same constraint: no phrasing from its
    // forbidden list, and no claim that a session is protected when nobody checked.


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

    /// <summary>The DM's pending-request prompts. Its own surface; see <see cref="AdmissionPromptView"/>.</summary>
    private readonly AdmissionPromptView _admissionPrompts;

    private string _codeEntry = string.Empty;
    private string _nameEntry = string.Empty;
    private string _seededFrom = string.Empty;

    /// <param name="coordinator">The session layer this window reflects.</param>
    /// <param name="displayName">What to call ourselves when joining (R-1.3e). Asked each time.</param>
    public SessionWindow(SessionCoordinator coordinator, Func<DisplayName> displayName)
        : base("Dungeon Master XIV session###dmx-session")
    {
        _coordinator = coordinator;
        _displayName = displayName;
        _admissionPrompts = new AdmissionPromptView(coordinator);
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

        // Drawn last and from here rather than from inside DrawJoining, where it used to live: these
        // are the DM's prompts, and a joiner's method was never their place. Composed rather than
        // called through, so the two surfaces can be read and changed independently.
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

        // R-1.3h: a client that has joined a session offers NO WAY to host one. The control is
        // ABSENT rather than disabled — the requirement is explicit that a greyed control which
        // still occupies the UI fails, because it invites exactly the question the exclusivity
        // exists to remove.
        if (!InAJoinedSession())
        {
            if (ImGui.Button("Start session"))
            {
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
    private bool InAJoinedSession() =>
        _coordinator.Join.Phase is JoinPhase.Contacting or JoinPhase.AwaitingDecision or JoinPhase.Admitted;

    /// <summary>
    /// Whether this client is hosting — from claiming a code until the session ends (R-1.3h).
    /// </summary>
    /// <remarks>
    /// <c>Registering</c> counts. A code has been claimed and the session is being established, so
    /// offering a join there would let a DM start hosting and join someone else in the window
    /// before registration completes — which is the state the criterion calls "the life of the
    /// session". <c>Failed</c> does not: nothing was established, and the client is free again.
    /// </remarks>
    private bool InAHostedSession() =>
        _coordinator.Host.Phase is HostingPhase.Registering or HostingPhase.Hosting;

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

        // R-1.3h, the other direction: while this client is hosting there is no way to join. The
        // code box goes with the button — leaving a field to type into is still offering the way.
        if (!InAHostedSession() && (join.MayRequestAgain || join.Phase == JoinPhase.Denied))
        {
            ImGui.InputText("Session code", ref _codeEntry, 16);

            // A-1.2n: the name that will be sent is shown and editable HERE, on the screen the user
            // is already on. A build whose only name control is in settings fails the criterion
            // however well the settings work, because a user who never opens settings never learns
            // what is about to be sent on their behalf. The settings value pre-fills this; it does
            // not replace it.
            SeedNameFromSettings();
            ImGui.InputText("Name they will see", ref _nameEntry, DisplayName.MaxLength + 1);

            if (ImGui.Button("Request to join") && SessionCode.TryParse(_codeEntry, out var code))
            {
                // R-1.3e: we name ourselves on the request, so the DM's prompt has a name without a
                // second round trip. It is a label and never a credential — the fingerprint the DM
                // compares is what decides, and it is unaffected by whatever this returns.
                //
                // Sent from the FIELD, not from settings. If those two could disagree the control
                // above would be decoration, which is the precise failure A-1.2n names.
                _coordinator.RequestJoin(code, DisplayName.OrNone(_nameEntry));
            }
        }

        if (join.Failure != SessionFailure.None)
        {
            ImGui.TextWrapped(SessionFailureMessage.For(join.Failure));
        }

    }

    /// <summary>
    /// Pre-fills the name field from settings, without ever overwriting what the user typed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pre-fill, not replace (A-1.2n).</b> A settings default that seeds this field passes; a
    /// settings control that stands in for it does not. So this seeds and then gets out of the way.
    /// </para>
    /// <para>
    /// <b>Re-seeds when the SOURCE changes and the user has not edited</b>, which is the case a
    /// once-only seed gets wrong: a player who switches character mid-session would otherwise sit
    /// looking at the previous character's name and send it. Comparing against what was last seeded
    /// rather than against the current settings value is what distinguishes "untouched" from
    /// "deliberately typed back to the default" — the second must survive, because a user who typed
    /// it means it.
    /// </para>
    /// <para>
    /// <b>It does not re-decide T-32's rules.</b> Whether the stored alias or the character name is
    /// the right source is settled before this sees it; this consumes one value and never inspects
    /// why it is that value.
    /// </para>
    /// </remarks>
    private void SeedNameFromSettings()
    {
        var fromSettings = _displayName().Value;

        if (fromSettings == _seededFrom || _nameEntry != _seededFrom)
        {
            return;
        }

        _nameEntry = fromSettings;
        _seededFrom = fromSettings;
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
