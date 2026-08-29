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

    /// <summary>
    /// Whether the DM is offered the confirmation control at all (A-1.2f).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Suppressed ONLY on positive evidence the joiner could not compare</b>, because there is
    /// nothing for them to read back and a tick would record a comparison that could not have
    /// happened. Never on <see cref="ComparabilityEvidence.NotEstablished"/> — A-1.2o fails a build
    /// that suppresses on silence, and silence is the ORDINARY case: a fast admission (A-1.2p)
    /// decides before the receipt could arrive.
    /// </para>
    /// <para>
    /// <b>NOTHING RETURNS FALSE TODAY, and that is recorded rather than overlooked.</b>
    /// <see cref="ComparabilityEvidence.EstablishedIncapable"/> has no producer — D-14 makes the
    /// pending notice additive, so a client that ignores it carries the same version and is refused
    /// by nothing. <b>A-1.2f's suppression is therefore unreachable and its QUALIFIED branch is the
    /// live one</b>; see <see cref="ComparabilityNote"/>, which is where this actually shows up on a
    /// DM's screen today.
    /// </para>
    /// </remarks>
    /// <param name="request">The pending request.</param>
    public static bool OffersConfirmation(PendingAdmission request) =>
        request is not null
        && request.Comparability != ComparabilityEvidence.EstablishedIncapable;

    /// <summary>
    /// What the prompt tells the DM about whether the joiner can compare at all (A-1.2o), or an
    /// empty string when there is nothing to qualify.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The "not established" sentence asserts NEITHER direction, and that is the whole
    /// criterion.</b> A-1.2o: <i>where the host has not established whether the joiner could
    /// compare, the UI says so</i>. Saying nothing fails it as surely as saying "they cannot" does —
    /// a DM shown a bare tickbox reads it as an ordinary comparison, which is the false record
    /// BUG-33 produced.
    /// </para>
    /// <para>
    /// <b>It must not read as suspicion.</b> qa-2 measured a 171ms admission producing zero receipts
    /// from a joiner that could compare perfectly well, so the common reason for this sentence is
    /// that the DM was quick — not that anything is wrong. Wording that implied an attack would
    /// train DMs to ignore it, and then it is not a signal on the day it means something.
    /// </para>
    /// <para>
    /// <b>Empty for <see cref="ComparabilityEvidence.EstablishedCapable"/>, deliberately.</b> The
    /// ordinary prompt already tells the DM to compare out of band; adding "and they can see it"
    /// beside every capable joiner is noise that would bury the sentence above.
    /// </para>
    /// </remarks>
    /// <param name="request">The pending request.</param>
    public static string ComparabilityNote(PendingAdmission request) =>
        request?.Comparability switch
        {
            ComparabilityEvidence.NotEstablished => NotEstablished,
            ComparabilityEvidence.EstablishedIncapable => Incapable,
            _ => string.Empty,
        };

    private const string NotEstablished =
        "We have not heard whether this player's client can show them a code to read back. That is "
        + "neither a yes nor a no - a quick decision usually beats the message. Compare out of band "
        + "as usual, and only tick the box if they actually read the code back to you.";

    private const string Incapable =
        "This player's client reported that it cannot show them a code, so there is nothing for them "
        + "to read back and nothing to confirm. Admitting them leaves this session unprotected "
        + "against someone sitting in the middle of it.";
}
