using System;
using System.Collections.Generic;
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

    // R-1.3f. NARROWER THAN "everyone in this session", and deliberately so: the roster a player
    // receives structurally omits the DM (DMXENG-33), so the broader wording would assert the DM is
    // absent rather than merely not list them. The claim is kept true of exactly what is shown.
    private const string PlayersInThisSession = "Players in this session:";

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

            // R-1.3f / A-1.13: the DM sees the display NAMES of everyone currently admitted. The
            // count above is not that -- it says how many without saying who, which is the half the
            // criterion is about. Read from the audience rather than from the broadcast roster,
            // because the host AUTHORS the roster (D-3) and never receives one.
            DrawRoster(audience.Recipients.Select(peer => (peer.DisplayName.Value, peer.Role)));

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

    /// <summary>Renders who is in the session, for whichever side is asking.</summary>
    /// <remarks>
    /// <para>
    /// <b>One renderer for both views on purpose.</b> The DM reads its own audience and a player
    /// reads the roster the host sent, so the two arrive as different types from different places —
    /// but what a participant LOOKS like must not depend on which side is drawing them. Two
    /// renderers would be two places for the unknown-role rule to drift.
    /// </para>
    /// <para>
    /// <b>An unrecognised role renders no label and the participant still appears</b>, per
    /// <see cref="SessionRoleLabel"/>. The reasoning lives there because it is a decision about
    /// meaning rather than about drawing.
    /// </para>
    /// </remarks>
    private static void DrawRoster(IEnumerable<(string Name, SessionRole Role)> participants)
    {
        foreach (var (name, role) in participants)
        {
            // The name is a label and never an identity: names are self-declared and two people may
            // hold the same one (A-1.2d), so nothing here keys on it or de-duplicates by it.
            ImGui.TextUnformatted(
                SessionRoleLabel.For(role) is { } label ? $"  {name} ({label})" : $"  {name}");
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

        // R-1.3f / A-1.13a: a joined player renders the roster the HOST authored and never
        // originates one. Rebuilding on reconnect needs nothing here: the host republishes on
        // admission rather than on change, so a client that comes back is re-admitted and sent the
        // current roster instead of starting empty and waiting for changes it missed.
        //
        // A-1.14 is satisfied UPSTREAM, not by a check on this line. RelayRouter.ForwardPayload
        // never forwards to a non-member, so an unadmitted client receives no roster and this
        // renders nothing -- absent from the payload rather than filtered in the UI, which is what
        // D-13's None requires and what makes the criterion assessable over what a client RECEIVES.
        if (join.Phase == JoinPhase.Admitted && _coordinator.Roster.Count > 0)
        {
            // THE HEADING IS A CLAIM, AND IT IS NARROWED ON PURPOSE. This roster structurally
            // omits the DM -- the host is not on its own Recipients, so it is never in what it
            // sends (DMXENG-33 is the other half). A region reading "everyone in this session"
            // would therefore TELL A PLAYER THE DM IS NOT HERE, which is a false statement to a
            // user rather than a missing feature. That is #89's defect one layer over: a control
            // labelled with a promise it does not keep.
            //
            // "Players" is true of exactly what is shown, today and after DMXENG-33 changes what
            // travels. The empty case renders nothing at all rather than a heading over no names,
            // which would assert there are no players while the reader is one.
            ImGui.TextUnformatted(PlayersInThisSession);
            DrawRoster(_coordinator.Roster.Select(entry => (entry.DisplayName, entry.Role)));
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

            // RESOLVED ONCE, then shown and sent. A-1.2n says the name that WILL BE SENT is shown,
            // so the box alone does not satisfy it: DisplayName refuses a large class of ordinary
            // invented names — Bob_123, Bob!, Bob (DM), an emoji — and a field showing one of those
            // beside a wire carrying "a player who gave no name" makes the criterion's own sentence
            // false, under a label that is literally the promise being broken.
            //
            // One value, used twice. The two cannot disagree by construction rather than by anyone
            // remembering to keep them in step.
            var willSend = DisplayName.OrNone(_nameEntry);

            ImGui.TextWrapped(willSend.WasStated
                ? $"They will see: {willSend.Value}"
                : $"That name cannot be sent, so they will see \"{DisplayName.Unstated}\". Letters, "
                  + "digits, spaces, apostrophes and hyphens work.");

            if (ImGui.Button("Request to join") && SessionCode.TryParse(_codeEntry, out var code))
            {
                // R-1.3e: we name ourselves on the request, so the DM's prompt has a name without a
                // second round trip. It is a label and never a credential — the fingerprint the DM
                // compares is what decides, and it is unaffected by whatever this returns.
                //
                // Sent from the same resolved value that was SHOWN, not re-resolved here: a second
                // call would be a second chance to disagree with the line above.
                _coordinator.RequestJoin(code, willSend);
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
    /// <b>The decision AND its invariant are <see cref="JoinFlowName"/>'s, not this method's.</b>
    /// The rule is untestable here — no test project links the plugin — and so was the pairing it
    /// rests on: the field and the seed it was written from must move together, and while this
    /// method assigned them separately that precondition sat exactly where the rule had just been
    /// taken from. Core returns both, so this is one destructuring assignment and there is no way
    /// to update one without the other.
    /// </remarks>
    private void SeedNameFromSettings() =>
        (_nameEntry, _seededFrom) = JoinFlowName.Resolve(_displayName().Value, _seededFrom, _nameEntry);

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
