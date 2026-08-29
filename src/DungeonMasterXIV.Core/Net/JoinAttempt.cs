using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A player's side of joining (R-1.3). Every state is one the UI can name, because R-1.3 requires
/// the player to know which one they are in rather than watching an ambiguous spinner.
/// </summary>
/// <remarks>
/// As with <see cref="HostSession"/>, elapsed time is passed in rather than read from the clock.
/// </remarks>
public sealed class JoinAttempt
{
    /// <summary>How long to wait for the relay before reporting it unreachable (A-1.5b).</summary>
    public static readonly TimeSpan ContactTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Where this attempt is.</summary>
    public JoinPhase Phase { get; private set; } = JoinPhase.Idle;

    /// <summary>The code being used, or null when idle.</summary>
    public SessionCode? Code { get; private set; }

    /// <summary>Why <see cref="JoinPhase.Failed"/>, or <see cref="SessionFailure.None"/>.</summary>
    public SessionFailure Failure { get; private set; } = SessionFailure.None;

    /// <summary>
    /// When this request lapses, as told to us by the DM's client. Null until the host answers.
    /// </summary>
    /// <remarks>
    /// <b>Given, never started here.</b> R-1.3c requires the admission wait and R-1.3a's prompt
    /// expiry to be the same window seen from two sides; a duration this client counted for itself
    /// would be a second clock, and the two drift on network delay, clock skew or a suspended
    /// client. The drift is not cosmetic — it produces a player told the request lapsed while the DM
    /// still holds a live prompt, so the DM accepts into nothing and neither side sees a fault.
    /// </remarks>
    public AdmissionDeadline? Deadline { get; private set; }

    /// <summary>
    /// Whether this client may receive session state.
    /// </summary>
    /// <remarks>
    /// True only while admitted. R-1.3 says a denied or pending client receives "no roster, no
    /// state, no events — not a filtered view, nothing", which is D-13's None level: absent from
    /// the payload rather than hidden on arrival.
    /// </remarks>
    public bool MayReceiveSessionState => Phase == JoinPhase.Admitted;

    /// <summary>
    /// The combined fingerprint to read aloud, or null while this client has nothing to compare.
    /// </summary>
    /// <remarks>
    /// Null is the honest answer and the UI must render it as one. A joiner shown a fingerprint they
    /// could not actually have compared is the failure BUG-31 was filed for, one screen over.
    /// </remarks>
    public string? Fingerprint { get; private set; }

    /// <summary>
    /// Whether a fingerprint was available <b>at the moment the DM admitted this client</b>
    /// (A-1.3f-1, R-1.3a-i).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Snapshotted in <see cref="Admitted"/>, never derived from <see cref="Fingerprint"/>.</b>
    /// That is the whole ordering guarantee, and the reason it is a stored bool rather than
    /// <c>Fingerprint is not null</c>: the host's key also arrives in the acceptance envelope, so a
    /// derived property would flip to true a moment after admission and report that a comparison was
    /// possible when it was not. A-1.3f-1 asks whether the key was available <i>beforehand</i>, and
    /// only a value captured before the transition can answer that.
    /// </para>
    /// <para>
    /// False after an admission by a host too old to send
    /// <see cref="WireMessageType.JoinPending"/>. That client is still admitted — D-14 requires an
    /// old host and a new joiner to interoperate — but the UI says the check could not be made
    /// rather than implying it was.
    /// </para>
    /// </remarks>
    public bool FingerprintWasComparableAtDecision { get; private set; }

    /// <summary>
    /// The participant the host says this client is, once it has been admitted and told (R-1.5c).
    /// Null until then, and null for a host that created none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>IN MEMORY ONLY, AND THAT IS A RULING RATHER THAN AN OMISSION.</b> SQ-53 ruled an
    /// in-memory receipt <b>CONFORMING</b> and left the split to the Engineering Lead, <b>which
    /// chose this cut</b> — SQ-53 does not forbid persistence, and saying it does would put somebody
    /// else's name on the Lead's decision. R-1.5b's
    /// obligations — the player can SEE what is stored and DELETE it per campaign, without the DM's
    /// involvement — attach to <b>persistence</b>, not to conveyance, because retention and deletion
    /// are meaningless for a value that dies with the process. So a receipt that lives only in this
    /// object conforms, and <b>the moment anything writes it to disk those obligations attach IN THE
    /// SAME CHANGE</b>, not in a follow-up ticket.
    /// </para>
    /// <para>
    /// <b>So this does NOT deliver R-1.5, and A-1.9g stays RED.</b> That criterion was tightened the
    /// same day to <i>retains it across a plugin restart</i>, precisely because <i>"and stores it"</i>
    /// is satisfied by an in-memory receipt while relink — which is by definition across launches —
    /// remains impossible. <b>If A-1.9g goes green on this, something is wrong.</b>
    /// </para>
    /// <para>
    /// <b>Not a credential.</b> Relink is DM-approved every time, so nothing is granted on holding
    /// this. R-1.5a's proof-of-possession governs <i>resume</i>, a different path.
    /// </para>
    /// </remarks>
    public Guid? ParticipantId { get; private set; }

    /// <summary>The player asked to join. A human action, never automatic (R-1.3).</summary>
    public void Request(SessionCode code)
    {
        Phase = JoinPhase.Contacting;
        Code = code;
        Failure = SessionFailure.None;
        Deadline = null;
        Fingerprint = null;
        FingerprintWasComparableAtDecision = false;

        // CLEARED WITH THE REST, and it is the one that would do damage if it were not. A new
        // attempt may be to a DIFFERENT session under a different host, and R-1.5b binds a stored
        // UUID under a session code -- carrying the previous host's answer into it would let this
        // client present one campaign's participant to another, which is the cross-campaign linkage
        // D-8 exists to refuse. It survives no longer than the attempt that was told it.
        ParticipantId = null;
    }

    /// <summary>
    /// The player has left this session deliberately (R-1.3g).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>To <see cref="JoinPhase.Idle"/>, and the phase is chosen for what it SAYS.</b> Three
    /// existing transitions would move the client out of the session, and each renders a sentence
    /// that is false for someone who chose to go: <see cref="Lapsed"/> is shown as "the DM did not
    /// answer in time", <see cref="Denied"/> as "not admitted", <see cref="Fail"/> as "stopped after
    /// a problem". <c>Idle</c> renders "not in a session", which is what happened.
    /// </para>
    /// <para>
    /// <b>AND THAT LAST STEP IS AN INFERENCE, NOT A CITATION.</b> R-1.3c forbids reporting a LAPSE
    /// as a REFUSAL; it does not name a leave. Reading it as also forbidding "a leave reported as a
    /// lapse" is mine, and the Spec Owner has not ruled on it. Recorded here rather than only in a
    /// PR body, because a prediction written in the past tense inside merged code becomes a premise
    /// and the next reader cannot tell which it was.
    /// </para>
    /// <para>
    /// <b>Idle also restores what leaving is FOR.</b> It satisfies
    /// <see cref="MayRequestAgain"/> by the existing predicate — no line there changes — so the
    /// player can join somewhere else at once, and it takes the phase out of the set
    /// <c>SessionInterruption.InAJoinedSession</c> reads, which is what lets R-1.3h stop suppressing
    /// the offer to host. R-1.3h says exclusivity ends on a deliberate quit; this is that quit.
    /// </para>
    /// <para>
    /// <b>Clears exactly what <see cref="Request"/> clears, including the participant id.</b> The
    /// reasoning there applies unchanged and more directly: a client that has left must not carry
    /// one host's answer into whatever it joins next, which is the cross-campaign linkage D-8
    /// refuses. Leaving is a stronger break than re-asking, not a weaker one.
    /// </para>
    /// </remarks>
    public void Left()
    {
        Phase = JoinPhase.Idle;
        Code = null;
        Failure = SessionFailure.None;
        Deadline = null;
        Fingerprint = null;
        FingerprintWasComparableAtDecision = false;
        ParticipantId = null;
    }

    /// <summary>
    /// The host offered its public key, so this client can compute the fingerprint (R-1.3a-i).
    /// </summary>
    /// <remarks>
    /// Ignored once a decision has been made. Accepting it later would overwrite null with a
    /// plausible-looking string and put a fingerprint on screen that nobody could have read aloud in
    /// time — the exact shape of a control that records a check which did not happen (D-11).
    /// </remarks>
    /// <param name="hostPublicKey">The host's SPKI public key, from a pending notice.</param>
    /// <param name="ownPublicKey">This client's own public key.</param>
    public void HostKeyOffered(byte[] hostPublicKey, byte[] ownPublicKey)
    {
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        ArgumentNullException.ThrowIfNull(ownPublicKey);

        if (Phase is not (JoinPhase.Contacting or JoinPhase.AwaitingDecision))
        {
            return;
        }

        Fingerprint = KeyFingerprint.Of(hostPublicKey, ownPublicKey);
    }

    /// <summary>
    /// The relay delivered the request and the DM has not decided yet.
    /// </summary>
    /// <param name="deadline">
    /// When the DM's client says the window closes. Carried so this client can show a countdown
    /// toward it — R-1.3c requires the player to see the wait is bounded <i>while it runs</i>, not
    /// only when it ends. Being told after fifteen silent minutes is better than silence and worse
    /// than knowing.
    /// </param>
    public void AwaitDecision(AdmissionDeadline? deadline = null)
    {
        if (Phase != JoinPhase.Contacting)
        {
            return;
        }

        Phase = JoinPhase.AwaitingDecision;
        Deadline = deadline;
    }

    /// <summary>
    /// The window closed with no answer (R-1.3c, A-1.5h).
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="Denied"/>. Nobody refused this player — the DM may have been
    /// mid-encounter — so asking again is reasonable and the UI must say so. Reporting a lapse as a
    /// refusal tells someone they were turned away when nobody looked, and the two call for
    /// different behaviour: a lapsed player may re-request, a denied one should not be invited to.
    /// </remarks>
    public void Lapsed()
    {
        Phase = JoinPhase.Lapsed;
        Failure = SessionFailure.None;
    }

    /// <summary>
    /// Whether this attempt can simply be tried again without a new code (A-1.5h).
    /// </summary>
    public bool MayRequestAgain => Phase is JoinPhase.Lapsed or JoinPhase.Failed or JoinPhase.Idle;

    /// <summary>How long the player has left, for the countdown. Zero when there is no deadline.</summary>
    public TimeSpan RemainingAt(DateTimeOffset now) =>
        Deadline?.RemainingAt(now) ?? TimeSpan.Zero;

    /// <summary>The DM accepted.</summary>
    /// <remarks>
    /// Captures whether a fingerprint existed <b>before</b> this transition (A-1.3f-1). Read the
    /// remarks on <see cref="FingerprintWasComparableAtDecision"/> for why the capture has to happen
    /// here rather than being computed on demand afterwards.
    /// </remarks>
    public void Admitted()
    {
        if (Phase != JoinPhase.AwaitingDecision)
        {
            return;
        }

        FingerprintWasComparableAtDecision = Fingerprint is not null;
        Phase = JoinPhase.Admitted;
    }

    /// <summary>
    /// The host has told this client which participant it is (R-1.5c). Ignored unless this client
    /// is admitted.
    /// </summary>
    /// <remarks>
    /// <b>The phase guard is R-1.3b, not tidiness.</b> An unadmitted client receives no session
    /// traffic at all, so a participant id arriving before the decision is one this client was never
    /// entitled to — and accepting it would record an identity granted by a message rather than by
    /// an admission. Silently ignored rather than failed: the frame is dropped exactly as any
    /// unusable input on this path is, and the join itself is unaffected.
    /// </remarks>
    /// <param name="participantId">The participant the host created.</param>
    public void ToldItIsParticipant(Guid participantId)
    {
        if (Phase != JoinPhase.Admitted)
        {
            return;
        }

        ParticipantId = participantId;
    }

    /// <summary>
    /// The DM declined, or removed this client mid-session. Both end in the same place: no session
    /// state flows from this point, and R-1.3 requires the player to be told rather than dropped.
    /// </summary>
    public void Denied()
    {
        Phase = JoinPhase.Denied;
        Failure = SessionFailure.None;
    }

    /// <summary>Abandons the attempt without a decision having been made.</summary>
    public void Fail(SessionFailure failure)
    {
        Phase = JoinPhase.Failed;
        Failure = failure;
    }

    /// <summary>
    /// Fails the attempt if the relay has not answered in time.
    /// </summary>
    /// <remarks>
    /// Only <see cref="JoinPhase.Contacting"/> times out. Waiting on the DM does not, because a DM
    /// taking a minute to decide is normal and telling the player their attempt failed would be
    /// false — R-1.3's requirement is that the player knows they are waiting on a person, which is
    /// what <see cref="JoinPhase.AwaitingDecision"/> says.
    /// </remarks>
    /// <param name="elapsedSinceRequest">How long the relay has had the request.</param>
    /// <returns>True if this call ended the attempt.</returns>
    public bool ExpireIfContactTimedOut(TimeSpan elapsedSinceRequest)
    {
        if (Phase != JoinPhase.Contacting || elapsedSinceRequest < ContactTimeout)
        {
            return false;
        }

        Fail(SessionFailure.RelayUnreachable);
        return true;
    }
}
