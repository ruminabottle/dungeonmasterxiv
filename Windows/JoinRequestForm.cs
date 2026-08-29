using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// Composing a request to join: the code, the name that will be sent, and the button (R-1.3, R-1.3e).
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="JoinFlowView"/> by DMXENG-75, and it is a PURE MOVE.</b> No behaviour
/// changes here and no criterion is claimed. <c>JoinFlowView.Draw</c> was 121 lines against a
/// 60-line method block — grandfathered by the delta gate, which forbids making it worse — and
/// R-1.3g's client half has to add to that surface.
/// </para>
/// <para>
/// <b>The seam is INPUT versus STATE, and the fields are what prove it.</b> Every mutable field
/// <see cref="JoinFlowView"/> had — the code box, the name box and the seed marker — belonged to
/// this form and to nothing else; what remains there reads the attempt and renders it, holding no
/// state at all. A view that owns no input and a form that owns nothing but is the same split
/// PR #89 made when the DM's side left for <see cref="AdmissionPromptView"/>.
/// </para>
/// <para>
/// <b>Whether this may be shown at all is NOT decided here.</b> R-1.3h — a hosting client offers no
/// way to join, and the affordance is absent rather than disabled — stays with the caller, because
/// it is a decision about what the window OFFERS rather than about how a request is composed. The
/// code box goes with the button: leaving a field to type into is still offering the way.
/// </para>
/// </remarks>
internal sealed class JoinRequestForm
{
    // A-1.2v (BUG-92). SAID IN BOTH PLACES A NAME IS TYPED, deliberately: a joiner who never opens
    // settings meets this box and no other, so a message that lived only in ConfigWindow would leave
    // the criterion unmet on the surface most people actually use — the same argument A-1.2n makes
    // for the name control itself being here.
    //
    // Not R-1.7a copy — R-1.7a covers the session window, the admission prompt and settings, and does
    // not supply wording for this. Written under the same constraint: no phrasing from its forbidden
    // list, and no claim that a session is protected when nobody checked.
    //
    // The conditional is load-bearing. A full box means nothing MORE will be accepted; it does not
    // mean anything was lost, because a user who typed to the ceiling and stopped lost nothing.
    // Duplicated as a literal rather than shared with ConfigWindow: these are two audiences and the
    // wording is free to diverge, and a shared constant would quietly forbid that.
    private const string NameFieldIsFull =
        "This box is full and will not take any more. If you were still typing, the rest did not go "
        + "in - use a shorter name.";

    private readonly SessionCoordinator _coordinator;

    /// <summary>
    /// What to call ourselves when asking to join (R-1.3e). A function rather than a value because
    /// the answer changes with who is logged in, and a name captured once is stale exactly when a
    /// player switches character — see <c>LocalCharacterName</c>.
    /// </summary>
    private readonly Func<DisplayName> _displayName;

    private readonly Func<RelinkMemory> _relink;

    private string _codeEntry = string.Empty;
    private string _nameEntry = string.Empty;
    private string _seededFrom = string.Empty;

    /// <param name="coordinator">The session layer this form asks to join.</param>
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
    public JoinRequestForm(
        SessionCoordinator coordinator,
        Func<DisplayName> displayName,
        Func<RelinkMemory> relink)
    {
        _coordinator = coordinator;
        _displayName = displayName;
        _relink = relink;
    }

    /// <summary>Draws the code box, the name box and the button that sends the request.</summary>
    public void Draw()
    {
        ImGui.InputText("Session code", ref _codeEntry, 16);

        // A-1.2n: the name that will be sent is shown and editable HERE, on the screen the user
        // is already on. A build whose only name control is in settings fails the criterion
        // however well the settings work, because a user who never opens settings never learns
        // what is about to be sent on their behalf. The settings value pre-fills this; it does
        // not replace it.
        SeedNameFromSettings();
        ImGui.InputText("Name they will see", ref _nameEntry, DisplayName.MaxUtf8Bytes);

        // RESOLVED ONCE, then shown and sent. A-1.2n says the name that WILL BE SENT is shown,
        // so the box alone does not satisfy it: DisplayName refuses a large class of ordinary
        // invented names — Bob_123, Bob!, Bob (DM), an emoji — and a field showing one of those
        // beside a wire carrying "a player who gave no name" makes the criterion's own sentence
        // false, under a label that is literally the promise being broken.
        //
        // One value, used twice. The two cannot disagree by construction rather than by anyone
        // remembering to keep them in step.
        var willSend = DisplayName.OrNone(_nameEntry);

        // A-1.2v (BUG-92): the field stopping is told, not left to be noticed. SEPARATE from the
        // line below, which is about whether the name can be SENT -- a full box is not an
        // invalid name, and what is in it may resolve perfectly. Both can be true at once and
        // they answer different questions, so neither is an else-branch of the other.
        if (NameInputCapacity.IsFull(_nameEntry))
        {
            ImGui.TextWrapped(NameFieldIsFull);
        }

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
            // R-1.5b's CARRYING half, and the line DMXENG-1 exists for: until now the only
            // production caller passed two arguments, so claimedParticipantId was null on every
            // join the shipped build made and relink was unreachable however much of it existed.
            //
            // Null when we have never been admitted under this code, or when the player has
            // deleted it -- both mean "join as a stranger", which is what makes the deletion in
            // the settings window actually undo the relink rather than merely hide it.
            _coordinator.RequestJoin(code, willSend, _relink().IdFor(code));
        }
    }

    // Expression-bodied ON PURPOSE, and it is load-bearing rather than terse: it keeps both
    // assignments in one statement. Give this a block body and assign only the field —
    //     { _nameEntry = JoinFlowName.Resolve(...).Entry; }
    // — and it builds, and the suite passes, while _seededFrom freezes and the pre-fill silently
    // stops following a character switch.
    private void SeedNameFromSettings() =>
        (_nameEntry, _seededFrom) = JoinFlowName.Resolve(_displayName().Value, _seededFrom, _nameEntry);
}
