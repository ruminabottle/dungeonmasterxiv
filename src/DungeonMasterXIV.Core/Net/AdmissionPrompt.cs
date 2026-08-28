namespace DungeonMasterXIV.Net;

/// <summary>
/// What the DM's admission prompt says about a pending request.
/// </summary>
/// <remarks>
/// <para>
/// In Core so the wording is under test rather than only looked at, and so the one place a relink
/// differs from a join is a string this file returns.
/// </para>
/// <para>
/// <b>A resolved relink changes this sentence and nothing else.</b> It does not remove the
/// fingerprint comparison, does not pre-confirm it, does not add a shortcut, and does not admit
/// anyone — R-1.5 requires the DM to approve every relink, every session. If a future change makes
/// a relink take fewer steps than a join, this type is not where that happens and it must not
/// become where it happens.
/// </para>
/// </remarks>
public static class AdmissionPrompt
{
    /// <summary>
    /// Which answer, if any, the prompt should start with selected. <b>Always
    /// <see cref="AdmissionAction.None"/>, for every request, including a resolved relink.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pre-selecting an answer breaks R-1.5 as surely as auto-admitting does</b>, and it is the
    /// version somebody would actually write — not <c>if (match) Admit()</c>, but a prompt that
    /// opens with Accept focused, having recognised the returning player and being helpful about
    /// it. A DM who pressed Enter on a pre-selected Accept leaves the same record as a DM who
    /// compared the fingerprint out of band, and R-1.3a's whole design rests on those two being
    /// distinguishable. The nudge does not weaken the check slightly; <b>it makes the record of the
    /// check false.</b>
    /// </para>
    /// <para>
    /// This exists as a method returning a constant so that a future change wanting to be helpful
    /// has to come here and say so, where a test is pinning it. <b>What it cannot constrain</b> is a
    /// window calling ImGui's focus API directly — that stays a review question, and this method is
    /// not a substitute for asking it.
    /// </para>
    /// </remarks>
    /// <param name="request">The pending request. Its content changes nothing.</param>
    public static AdmissionAction Favoured(PendingAdmission request) => AdmissionAction.None;

    /// <summary>
    /// The headline for a request: what they call themselves, and the code that tells two of them
    /// apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The display name is shown and the peer code is kept, and both halves are deliberate
    /// (R-1.3e, D-8 as amended).</b> Showing the name is the point of the requirement. Keeping the
    /// code is what makes A-1.2d hold: names are self-declared and nothing prevents two requesters
    /// sending the same one, so a headline carrying only a name would render two pending requests
    /// identically and the DM could not tell which they were admitting.
    /// </para>
    /// <para>
    /// <b>The relink label and the display name are different things and must not be conflated.</b>
    /// The label is resolved by this client from its own campaign store; the display name is a
    /// string the requesting client sent. A resolved relink still shows the name it sent, so a
    /// returning player who has renamed themselves does not read as two people.
    /// </para>
    /// <para>
    /// <b>Neither authenticates.</b> The fingerprint does, and the prompt renders it immediately
    /// below this line. A UI that shows this headline while omitting or de-emphasising the
    /// fingerprint is denied — D-11's substitution attack through a friendly label.
    /// </para>
    /// </remarks>
    /// <param name="request">The pending request.</param>
    public static string Headline(PendingAdmission request) =>
        request.Relink is { Matched: true, Label: { Length: > 0 } label }
            ? $"{request.DisplayName} ({request.PeerCode}) is asking to relink as {label}"
            : $"{request.DisplayName} ({request.PeerCode}) is asking to join";
}
