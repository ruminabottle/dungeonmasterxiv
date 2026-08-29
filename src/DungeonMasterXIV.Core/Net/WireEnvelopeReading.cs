using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What a received <see cref="WireEnvelope"/> may be believed to MEAN. The reading half of the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>The seam is outbound versus inbound, not size.</b> <see cref="WireEnvelope"/> is what a sender
/// builds and what arrives — shape, fields, factories. These answer a different question: given an
/// envelope from a party we do not control, <i>what is a receiver entitled to conclude?</i> Every one
/// of them is a gate that returns null rather than a getter that returns data, and each null is a
/// rule (D-11, R-1.3b, D-14) rather than an absence.
/// </para>
/// <para>
/// <b>Extension methods so that not one call site moves.</b> Roughly thirty callers across the
/// product and the suite keep reading <c>envelope.TryGetAdmissionOutcome(key)</c> exactly as before.
/// A refactor that renames thirty call sites to satisfy a line count would be the churn the limit is
/// supposed to prevent, and it would bury the two real changes in this PR.
/// </para>
/// <para>
/// <b>WHY NOW, HONESTLY.</b> Not because anyone spotted the boundary in advance. DMXENG-47 and
/// BUG-85 each fit alone and together put the class at <b>426 against a 400 block</b>; the limit
/// forced a cut and this is where the Code Reviewer measured the cheapest one — 426 to 329, under
/// the flag rather than merely under the block, with no new concept introduced. <b>The reviewer also
/// recorded that this partly reverses their own position on #88</b>, where they argued factories and
/// accessors co-vary; what changed is that the block is now breached rather than approached.
/// </para>
/// <para>
/// <b><see cref="ParticipantReceipt"/> is deliberately NOT folded in here, and it is a fair question
/// whether it should be.</b> It is named for the thing it reads rather than for the mechanics of
/// reading, and its three checks carry a requirement's worth of reasoning. Two homes for one category
/// is a smell; one type named for a concept beside a bag of readers is arguably right. <b>I have left
/// it and am flagging the choice rather than settling it silently</b> — folding it in is a two-line
/// change if the reviewer prefers.
/// </para>
/// </remarks>
public static class WireEnvelopeReading
{
    /// <summary>
    /// The host's public key offered <b>before</b> a decision, or null if this envelope is not a
    /// pending notice.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="TryGetAdmissionOutcome"/> and deliberately not an
    /// <see cref="AdmissionOutcome"/>: this message carries no decision, and folding it into the
    /// outcome vocabulary would let a consumer treat "the DM is looking at your request" as an
    /// answer. The distinction is the whole requirement.
    /// </remarks>
    public static byte[]? TryGetPendingHostKey(this WireEnvelope envelope) =>
        envelope.Type == WireMessageType.JoinPending ? envelope.HostPublicKey : null;

    /// <summary>
    /// The joiner's key from a fingerprint receipt, or null if this is not one.
    /// </summary>
    /// <remarks>
    /// Deliberately not folded into <see cref="TryGetAdmissionOutcome"/>: this decides nothing about
    /// the admission, and a consumer that could read it as an outcome would be reading a capability
    /// as an answer.
    /// </remarks>
    public static byte[]? TryGetFingerprintReceiptKey(this WireEnvelope envelope) =>
        envelope.Type == WireMessageType.JoinerHoldsFingerprint ? envelope.PublicKey : null;

    /// <summary>
    /// The admission outcome this envelope expresses <b>for the client whose key is
    /// <paramref name="ownPublicKey"/></b>, or null if it is not an admission answer or is not
    /// addressed to that client. Consumers go through <see cref="AdmissionOutcome.Match{T}"/>, so none can drop a case.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>BUG-85 (D-11): every admission answer is addressed, and nothing read the address.</b> All
    /// three carry the joiner's key so it can be matched to an attempt, and this returned an outcome
    /// without looking — so a client could reach <c>Admitted</c>, with a derived key, on an
    /// acceptance meant for somebody else. An honest relay resolves the addressee and forwards to it
    /// alone, so a mis-addressed answer arrives from the relay position D-11 assumes an attacker may
    /// hold.
    /// </para>
    /// <para>
    /// <b>All three arms, and the reported one is the least exposed:</b> <c>Admitted()</c> has a
    /// phase guard that narrows it to a joiner already awaiting a decision, while <c>Denied()</c> and
    /// <c>Lapsed()</c> have none. Guarding only what was reported is the shape BUG-56 rejects, and
    /// here it would have left the two EASIER arms open.
    /// </para>
    /// <para>
    /// <b>Dropped, not failed — the opposite of the ruling one arm inward, deliberately.</b>
    /// <c>AdmissionInbox</c> FAILS an unusable acceptance (BUG-59) because nothing lapses a joiner
    /// locally, so dropping would leave it awaiting an answer that already came. That turns on the
    /// answer being THIS CLIENT'S. Somebody else's says nothing about this attempt — the host may
    /// still be deciding, so remaining in <c>AwaitingDecision</c> is the true state. Failing would
    /// also let anyone who can post a frame end any joiner's attempt by naming a stranger, which
    /// makes dropping a correctness question rather than a preference between two safe answers.
    /// </para>
    /// <para>
    /// <b>Fixed-time, and required rather than defaulted.</b> The comparison matches
    /// <c>ParticipantReceipt.TryRead</c> — the framework primitive used directly, because wrapping it
    /// would be a second helper for one comparison. Required because any default would be "refuse
    /// everything" or "check nothing", and both are answers a caller should give on purpose.
    /// </para>
    /// </remarks>
    /// <param name="envelope">What arrived.</param>
    /// <param name="ownPublicKey">This client's own join key. Null is not addressable, so it decides
    /// nothing.</param>
    public static AdmissionOutcome? TryGetAdmissionOutcome(this WireEnvelope envelope, byte[]? ownPublicKey)
    {
        // The type arms are UNCHANGED and the address is checked on their RESULT, not ahead of them.
        // Gating first would make "not an answer" and "not my answer" the same null, so the tests
        // pinning the first would start passing for the second while still claiming the first. It
        // also means an arm added later inherits the check rather than having to remember it.
        return envelope.TryReadAdmissionAnswer() is { } outcome && IsAddressedTo(envelope, ownPublicKey)
            ? outcome
            : null;
    }

    /// <summary>
    /// The admission answer this envelope carries, <b>ignoring who it is addressed to</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE ARMS LIVE HERE ONCE SO A CALLER CAN TELL THE TWO NULLS APART (BUG-87).</b>
    /// <see cref="TryGetAdmissionOutcome"/> returns null for "not an admission answer" and for "an
    /// admission answer for somebody else" alike — deliberately, per the note above — which leaves a
    /// caller unable to distinguish an envelope worth remarking on from ordinary traffic. Asking
    /// this second question separates them.
    /// </para>
    /// <para>
    /// <b>Extracted rather than restated at the call site.</b> A copy of the three type arms would
    /// be a second list that agrees with this one only until an admission type is added — and the
    /// new arm would then be silently unobservable, which is the failure the observation exists to
    /// end. Nothing about the address rule moves: the check still runs on the RESULT of these arms.
    /// </para>
    /// <b>Not part of the public reading surface</b> — it answers "what does this say" without
    /// answering "is it mine", and only the addressed question is safe for a consumer to act on.
    /// </remarks>
    /// <param name="envelope">What arrived.</param>
    internal static AdmissionOutcome? TryReadAdmissionAnswer(this WireEnvelope envelope) =>
        envelope.Type switch
        {
            WireMessageType.JoinAccepted when envelope.HostPublicKey is not null => AdmissionOutcome.Accepted(envelope.HostPublicKey),
            WireMessageType.JoinDenied => AdmissionOutcome.Denied(),
            WireMessageType.JoinLapsed => AdmissionOutcome.Lapsed(),
            _ => null,
        };

    /// <summary>Whether this envelope names <paramref name="ownPublicKey"/> as its addressee.</summary>
    private static bool IsAddressedTo(WireEnvelope envelope, byte[]? ownPublicKey) =>
        envelope.PublicKey is { } addressee
        && ownPublicKey is not null
        && CryptographicOperations.FixedTimeEquals(addressee, ownPublicKey);

    /// <summary>The admission deadline carried here, if any.</summary>
    public static AdmissionDeadline? TryGetDeadline(this WireEnvelope envelope) =>
        envelope.DeadlineUtcTicks is { } ticks ? AdmissionDeadline.TryFromWire(ticks) : null;

    /// <summary>
    /// Recovers the sealed payload from a received envelope, or null if this is not a payload
    /// message or arrived without the fields one needs.
    /// </summary>
    public static SealedPayload? TryGetSealedPayload(this WireEnvelope envelope) =>
        envelope.Type == WireMessageType.SessionPayload && envelope.Nonce is not null && envelope.Payload is not null
            ? SealedPayload.FromWire(envelope.Nonce, envelope.Payload)
            : null;}
