using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// The joiner's surface: asking to join, waiting to be decided on, and being in a session
/// (R-1.3, R-1.3e, R-1.3h).
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="SessionWindow"/> by DMXENG-15, and it is a PURE MOVE.</b> No
/// behaviour changes here, no criterion is claimed, and nothing was fixed on the way past. The
/// seam is PR #89's, whose body is authoritative: <c>DrawJoining</c> had grown to carry five
/// things — phase reporting, the fingerprint comparison, the code box, the name box and the
/// failure line — and the DM's side had already left for
/// <see cref="AdmissionPromptView"/> for the same reason.
/// </para>
/// <para>
/// <b>Built by <see cref="SessionWindow"/> out of what it already holds</b>, exactly as
/// <see cref="AdmissionPromptView"/> is. That keeps <c>Plugin.cs</c> untouched: a constructor
/// parameter there would drag the composition root into a chunk that changes no behaviour.
/// </para>
/// <para>
/// <b>The copy travelled with the surface it belongs to.</b> The three constants below address the
/// person joining and nobody else, so <see cref="SessionWindow"/> no longer carries wording for a
/// screen it does not draw. The host's copy stayed with the host.
/// </para>
/// </remarks>
internal sealed class JoinFlowView
{
    // Not R-1.7a copy — R-1.7a covers the session window, the admission prompt and settings, and does
    // not supply wording for these. Written here under the same constraint: no phrasing from its
    // forbidden list, and no claim that a session is protected when nobody checked.
    //
    // Travelled with the constants it governs (DMXENG-15). It sat above all four in SessionWindow;
    // three came here and CodeChangedWarning stayed, so it is stated in both places rather than
    // left behind pointing at copy that had moved.

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

    private readonly SessionCoordinator _coordinator;

    /// <summary>
    /// What to call ourselves when asking to join (R-1.3e). A function rather than a value because
    /// the answer changes with who is logged in, and a name captured once is stale exactly when a
    /// player switches character — see <c>LocalCharacterName</c>.
    /// </summary>
    private readonly Func<DisplayName> _displayName;

    private string _codeEntry = string.Empty;
    private string _nameEntry = string.Empty;
    private string _seededFrom = string.Empty;

    /// <param name="coordinator">The session layer this surface reflects.</param>
    /// <param name="displayName">What to call ourselves when joining (R-1.3e). Asked each time.</param>
    public JoinFlowView(SessionCoordinator coordinator, Func<DisplayName> displayName)
    {
        _coordinator = coordinator;
        _displayName = displayName;
    }

    /// <summary>Draws the joiner's half of the session window.</summary>
    public void Draw()
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
            // The heading is a CLAIM about what is below it and the reasoning lives with the
            // value, in RosterHeading -- it is a decision about meaning, not about drawing. Held
            // in Core because a source check on a window constant was defeated by leaving the
            // constant honest and passing a literal here instead.
            //
            // The empty case renders nothing at all rather than a heading over no names, which
            // would assert there are no players while the reader is one.
            ImGui.TextUnformatted(RosterHeading.Text);
            RosterView.Draw(_coordinator.Roster.Select(entry => (entry.DisplayName, entry.Role)));
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

            // JoinFlowCode.Accepts, not SessionCode.TryParse inline (DMXENG-15). The decision about
            // what this field takes is Core's, so a test can call the same thing this button calls
            // instead of re-deriving it and claiming the two agree in a comment.
            if (ImGui.Button("Request to join") && JoinFlowCode.Accepts(_codeEntry, out var code))
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
    // RECORDED, NOT REDESIGNED (BUG-64, qa-3). Returning the pair makes correct use a one-liner; it
    // does NOT make misuse impossible. Give this a block body and assign only the field —
    //     { _nameEntry = JoinFlowName.Resolve(...).Entry; }
    // — and it builds, and the suite passes, while _seededFrom freezes and the pre-fill silently
    // stops following a character switch. The expression body is what keeps both assignments in one
    // statement, so it is load-bearing rather than terse. Left as an observation because closing it
    // properly is a seam change and this ticket is a test fix.
    private void SeedNameFromSettings() =>
        (_nameEntry, _seededFrom) = JoinFlowName.Resolve(_displayName().Value, _seededFrom, _nameEntry);

    // Every phase gets a sentence. R-1.3 forbids leaving anyone looking at an ambiguous spinner,
    // so there is no state here that renders as "..." and nothing else.
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
