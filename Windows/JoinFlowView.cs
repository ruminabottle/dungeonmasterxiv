using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
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
/// <b>The copy travelled with the surface it belongs to.</b> The constants below address the person
/// joining and nobody else, so <see cref="SessionWindow"/> no longer carries wording for a screen it
/// does not draw. The host's copy stayed with the host, and the name-field wording left again with
/// <see cref="JoinRequestForm"/> for the same reason.
/// </para>
/// </remarks>
internal sealed class JoinFlowView
{
    private readonly SessionCoordinator _coordinator;

    private readonly JoinComparisonView _comparison = new();

    private readonly SessionEndingView _ending;

    private readonly JoinRequestForm _requestForm;

    /// <param name="coordinator">The session layer this surface reflects.</param>
    /// <param name="displayName">What to call ourselves when joining (R-1.3e). Asked each time.</param>
    /// <param name="relink">
    /// What this client remembers about who it is, per session code (R-1.5b).
    /// <para>
    /// <b>A supplier rather than the object, so this reads it AT THE MOMENT OF THE JOIN.</b> The
    /// player may delete an entry from the settings window while this one is open, and a captured
    /// reference would let a join carry a claim the player had just removed — a deletion that
    /// appeared to work and did not.
    /// </para>
    /// </param>
    /// <param name="keepOrLose">
    /// Opens R-2.12's keep-or-lose choice over the session's log. Passed straight through to
    /// <see cref="SessionEndingView"/>, which is the joiner's side and the moment the offer belongs
    /// to; nothing here reads it.
    /// </param>
    public JoinFlowView(
        SessionCoordinator coordinator,
        Func<DisplayName> displayName,
        Func<RelinkMemory> relink,
        Func<SessionLogOffer> keepOrLose)
    {
        _coordinator = coordinator;
        _requestForm = new JoinRequestForm(coordinator, displayName, relink);
        _ending = new SessionEndingView(coordinator, keepOrLose);
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

        }

        _comparison.Draw(join);

        _ending.Draw(join);

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
        // code box goes with the button — leaving a field to type into is still offering the way,
        // and R-1.3h means the affordance is ABSENT rather than disabled-and-explained.
        //
        // The decision stays HERE and the composing left with DMXENG-75: what the window OFFERS is
        // this view's, how a request is built is the form's.
        if (!InAHostedSession() && (join.MayRequestAgain || join.Phase == JoinPhase.Denied))
        {
            _requestForm.Draw();
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
