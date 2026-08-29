using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The DM's side of who is in the session: who is asking, who is let in, and what each of them is
/// told.
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="SessionCoordinator"/> because that is where session-role behaviour
/// kept landing.</b> The first split (C25) took the transport concern; this one takes admission, and
/// it was chosen over the joiner half by arithmetic rather than taste. The coordinator had to shed
/// at least 98 lines to reach the 400-line limit; the whole joiner cluster is 76, so extracting it
/// could not have got there even at zero overhead. Admission is 156.
/// </para>
/// <para>
/// <b>It also answers the question a line count cannot: does the split remove the REASON lines
/// arrive?</b> The Tier 0 scope now landing — R-1.3f's roster and its D-13 access levels, R-1.3g's
/// departure removing a player from it — is about who is admitted and what others may see of them.
/// That is this type's subject, so it arrives here rather than in the coordinator.
/// </para>
/// <para>
/// <b>The host's code and keys are read through delegates rather than held.</b> Admission only means
/// anything while hosting, and both values change underneath it — a code is superseded on
/// reconnection (R-1.2a), and the key pair is disposed and replaced when a session restarts. A
/// captured copy would be a second source of truth for state whose whole point is that one client
/// owns it (D-3), and it would go stale silently.
/// </para>
/// </remarks>
public sealed class AdmissionControl
{
    private readonly AdmissionAnnouncer _announcer;
    private readonly Func<DisplayName, Guid?> _mintParticipant;
    private readonly ISessionTransportLog _log;
    private readonly Func<SessionCode?> _hostCode;
    private readonly Func<SessionKeyExchange?> _hostKeys;

    /// <param name="announcer">How answers reach a joiner. Owned by the caller.</param>
    /// <param name="hostCode">The session being hosted, read at each use rather than captured.</param>
    /// <param name="hostKeys">The host's ephemeral key pair, read at each use rather than captured.</param>
    /// <param name="mintParticipant">
    /// Creates a participant in the running campaign for a joiner about to be admitted, and returns
    /// its id (R-1.5c half 1). Returns null when there is no campaign to create one in.
    /// <para>
    /// <b>A DELEGATE RATHER THAN THE CAMPAIGN ITSELF, and the reason is not layering.</b> This type
    /// decides admissions; which campaign a session belongs to is settled elsewhere and can change
    /// under it. Taking a function keeps the question <i>who is joining what</i> answerable at the
    /// moment of admission rather than at construction — the same reason
    /// <paramref name="hostCode"/> is a function, and the same reason DMXENG-45 exists.
    /// </para>
    /// <para>
    /// <b>REQUIRED here and OPTIONAL on <see cref="SessionCoordinator"/>, which is not an
    /// inconsistency.</b> This type runs only on a host, so "no minter" is always a defect at this
    /// level. The coordinator is constructed by JOINING clients too — they host nothing, have no
    /// campaign, and would have to pass a meaningless delegate to satisfy a required parameter,
    /// across 23 files, to express a fact that is false for most of them.
    /// </para>
    /// <para>
    /// <b>So the silence is closed HERE, where the fact is knowable</b>: <see cref="Admit"/> warns
    /// on every admission that produced no participant, naming the peer code. A host wired without a
    /// minter is loud rather than quiet — which is the property the coordinator's <c>log</c>
    /// reasoning is actually protecting, reached by a different route.
    /// </para>
    /// </param>
    /// <param name="log">
    /// Where a joiner admitted <b>without</b> a participant is reported. <b>Required, not optional
    /// with a null default</b> — this is the exact shape PR #86's finding 5 was: a real loss that
    /// nobody was told about, surviving because the code that dropped it was under no obligation to
    /// speak.
    /// </param>
    public AdmissionControl(
        AdmissionAnnouncer announcer,
        Func<SessionCode?> hostCode,
        Func<SessionKeyExchange?> hostKeys,
        Func<DisplayName, Guid?> mintParticipant,
        ISessionTransportLog log)
    {
        ArgumentNullException.ThrowIfNull(mintParticipant);
        ArgumentNullException.ThrowIfNull(log);

        _announcer = announcer;
        _hostCode = hostCode;
        _hostKeys = hostKeys;
        _mintParticipant = mintParticipant;
        _log = log;
    }

    /// <summary>Who may receive session state. See <see cref="SessionAudience"/> for the D-13 levels.</summary>
    public SessionAudience Audience { get; } = new();

    /// <summary>The requests waiting on the DM (R-1.3).</summary>
    public AdmissionDesk Desk { get; } = new();

    /// <summary>
    /// Participants whose request lapsed on the most recent tick, so the caller can tell them it
    /// lapsed rather than leaving them waiting — and, per R-1.3c, never tell them they were denied.
    /// </summary>
    public IReadOnlyList<PendingAdmission> JustLapsed { get; private set; } = Array.Empty<PendingAdmission>();

    /// <summary>Records that a participant is asking to be let in.</summary>
    public void Receive(PendingAdmission request) => Desk.Receive(request);

    /// <summary>
    /// Turns a <see cref="WireMessageType.JoinRequest"/> that arrived on the wire into a prompt the
    /// DM can answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the arm BUG-42 was missing.</b> Everything below it existed and was tested; the
    /// relay forwarded every request to a host that had no path to it, so
    /// <see cref="Desk"/> stayed empty and no prompt was ever shown. Returning null when the
    /// host has no key is what keeps a joiner-only client from building prompts out
    /// of traffic meant for a host.
    /// </para>
    /// <para>
    /// <b>No relink claim is read here, deliberately.</b> The envelope can carry
    /// <c>ClaimedParticipantId</c>, and resolving it needs both ends of a conversation that does not
    /// exist yet (BUG-41). Passing the default is leaving that alone rather than half-building it.
    /// </para>
    /// </remarks>
    public void AdmitToTheQueue(byte[] joinerPublicKey, DateTimeOffset now, DisplayName displayName = default) =>
        Receive(PeerCodeFor(joinerPublicKey), joinerPublicKey, now, displayName: displayName);

    /// <summary>
    /// The session-scoped code the DM's prompt names this requester by (R-1.3, D-8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived from the joiner's key rather than assigned, because their key is the only thing
    /// that identifies them.</b> Nothing on the wire carries a peer code: a
    /// <see cref="WireMessageType.JoinRequest"/> carries the session code and the joiner's key, and
    /// the answer is addressed back by that same key. So a code invented here is the only one
    /// available, and deriving it keeps two requests from one joiner naming one requester.
    /// </para>
    /// <para>
    /// <b>Session-scoped because the session code is hashed in</b> — the same person joining two
    /// sessions is named differently in each, which is what D-8 asks of anything shown about a
    /// participant.
    /// </para>
    /// <para>
    /// <b>Not a security value and deliberately not the fingerprint.</b> The fingerprint is computed
    /// from BOTH keys and exists so two humans can compare one string; this only has to tell two
    /// requesters apart on one screen. <b>What the DM should actually see here is a product
    /// question</b> — PRD-1 requires a session-scoped code and does not say how it is formed, and
    /// nothing sends this code to the joiner, so the two of them cannot yet read the same label
    /// aloud. Raised with the Spec Owner rather than settled here.
    /// </para>
    /// </remarks>
    public PeerCode PeerCodeFor(byte[] joinerPublicKey)
    {
        var scope = System.Text.Encoding.UTF8.GetBytes(_hostCode()?.Value ?? string.Empty);
        var digest = System.Security.Cryptography.SHA256.HashData([.. scope, .. joinerPublicKey]);
        var value = new System.Numerics.BigInteger(digest, isUnsigned: true, isBigEndian: true);

        var rendered = new char[SessionCode.Length];
        for (var i = rendered.Length - 1; i >= 0; i--)
        {
            value = System.Numerics.BigInteger.DivRem(value, SpeakableAlphabet.Length, out var symbol);
            rendered[i] = SpeakableAlphabet.Characters[(int)symbol];
        }

        // Through the same gate a code off the wire goes through. The rendering above draws from
        // SpeakableAlphabet at SessionCode.Length, so this cannot fail today -- which is exactly why
        // it is checked here rather than trusted: if the two ever diverge, this throws at the source
        // instead of putting an ungeneratable code into a prompt and a roster.
        return PeerCode.FromGenerated(new string(rendered));
    }

    /// <summary>
    /// Builds and records a request from what arrived on the wire.
    /// </summary>
    /// <remarks>
    /// The fingerprint is computed here rather than passed in, so no caller can hand the prompt a
    /// string that does not correspond to the keys actually exchanged — a fingerprint that does not
    /// match the keys is worse than none, because the DM compares it and concludes it is safe.
    /// The deadline is decided here too: R-1.3c puts that decision on the DM's client (D-3), once.
    /// </remarks>
    public PendingAdmission? Receive(
        PeerCode peerCode,
        byte[] joinerPublicKey,
        DateTimeOffset now,
        RelinkClaim relink = default,
        DisplayName displayName = default)
    {
        if (_hostKeys() is not { } hostKeys)
        {
            return null;
        }

        var deadline = AdmissionDeadline.DecidedByHost(now);
        var request = new PendingAdmission(
            peerCode,
            KeyFingerprint.Of(joinerPublicKey, hostKeys.PublicKey),
            deadline,
            relink,
            joinerPublicKey,
            displayName);

        Desk.Receive(request);

        // The host's key goes back NOW, not on acceptance (R-1.3a-i, A-1.3f-1). Sending it here is
        // the entire fix: the joiner needs it while the DM is still deciding, because a fingerprint
        // that arrives with the answer cannot inform the answer. The same key travels again in
        // Admit's acceptance envelope, which is where it used to travel for the first time.
        if (_hostCode() is { } hostedCode)
        {
            _announcer.Pending(hostedCode, joinerPublicKey, hostKeys.PublicKey, deadline);
        }

        return request;
    }

    /// <summary>
    /// Admits the pending participant. Only after this does anything become addressable to them —
    /// see <see cref="SessionAudience"/>, which is where D-13's None level is enforced.
    /// </summary>
    /// <param name="peerCode">The requester's session-scoped code.</param>
    /// <param name="role">What they may do (E-11). Admission itself stays DM-only.</param>
    /// <remarks>
    /// Whether the DM compared the fingerprint is taken from the request rather than passed in, so
    /// an admission cannot be recorded as verified unless the DM actually said so (R-1.3a).
    /// </remarks>
    public AdmittedPeer Admit(PeerCode peerCode, SessionRole role = SessionRole.Player)
    {
        var request = Desk.Decide(peerCode);
        // The key and the name come from the request that is being answered, which is the only
        // place they exist. Taken here rather than defaulted, because a peer admitted without a key
        // is one the host can route to and never speak to — see AdmittedPeer.PublicKey.
        var peer = Audience.Admit(
            peerCode,
            role,
            request?.Verification ?? AdmissionVerification.NotCompared,
            request?.JoinerPublicKey,
            request?.DisplayName ?? DisplayName.None);

        // MINTED BEFORE THE ANSWER IS SENT, because the answer is what carries it (R-1.5c). The two
        // halves fail separately as requirements and cannot ship separately as work: an id nobody is
        // told is a wire whose middle does not exist, and a carrier with nothing to carry conveys
        // nothing. Both happen here, in that order, or neither does.
        //
        // THE NAME IS A LABEL AND NOTHING RESTS ON IT. It is self-declared (R-1.3e) and two joiners
        // may send the same one (A-1.2d); it exists so the DM can read its own roster later. The
        // participant is identified by its UUID, which this client mints and the joiner never
        // originates -- D-3, since a participant's roster identity is shared state.
        var participantId = _mintParticipant(peer.DisplayName);

        if (_hostCode() is { } code && _hostKeys() is { } hostKeys && request?.JoinerPublicKey is { } joinerKey)
        {
            _announcer.Accepted(code, joinerKey, hostKeys.PublicKey, participantId);
        }

        // A REAL LOSS, REPORTED RATHER THAN PASSED OVER -- PR #86's finding 5 in a new place. An
        // admitted player with no participant can never relink to this campaign: next session the
        // DM sees a stranger and approves them fresh, and NOTHING anywhere would have said why. The
        // peer code names WHICH person, because two may share a display name (A-1.2d) and D-8 keeps
        // a character name out of a log.
        if (participantId is null)
        {
            _log.Warning(
                $"Admitted {peerCode} without creating a participant, so this session has no "
                + "campaign to record them in. They will not be able to relink and will be a new "
                + "request next time.");
        }

        return peer;
    }

    /// <summary>
    /// Records that a joining client reported it holds the host key and can render a fingerprint
    /// (R-1.3a-iii), establishing <see cref="ComparabilityEvidence.EstablishedCapable"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The receipt names its sender by KEY, and the peer code is derived from it</b> — the same
    /// derivation the prompt was built with, so a receipt and the request it belongs to arrive at
    /// the same code without the wire ever carrying one. Nothing on the wire carries a peer code;
    /// see <see cref="PeerCodeFor"/>.
    /// </para>
    /// <para>
    /// <b>A receipt for a request that is not pending is IGNORED, not an error.</b> It is the
    /// ordinary consequence of a fast admission — the DM answers, the request leaves the desk, and
    /// the receipt arrives addressed to nobody. qa-2 measured a 171ms gap doing exactly that, so
    /// treating a late receipt as a fault would make the common case look broken.
    /// </para>
    /// <para>
    /// <b>This establishes state 1 and NOTHING ELSE (R-1.3a-iv).</b> It creates no producer for
    /// <see cref="ComparabilityEvidence.EstablishedIncapable"/> and therefore cannot make A-1.2f's
    /// suppression fire. <b>If anyone frames this arm as "fixing A-1.2f", that is the misreading to
    /// refuse</b> — it is incomplete rather than wrong, and the Spec Owner said so in those words.
    /// </para>
    /// </remarks>
    /// <param name="joinerPublicKey">The key the receipt was sent under.</param>
    public void RecordComparabilityReceipt(byte[] joinerPublicKey) =>
        Desk.Find(PeerCodeFor(joinerPublicKey))?.JoinerReportedItCanCompare();

    /// <summary>
    /// Declines the pending participant. Nothing was ever addressable to them, so there is nothing
    /// to withdraw — which is the point of admitting rather than filtering (R-1.3, D-13).
    /// </summary>
    public void Deny(PeerCode peerCode)
    {
        var request = Desk.Decide(peerCode);
        Audience.Remove(peerCode);

        if (_hostCode() is { } code && request?.JoinerPublicKey is { } joinerKey)
        {
            _announcer.Denied(code, joinerKey);
        }
    }

    private void AnnounceLapsed()
    {
        if (_hostCode() is { } code)
        {
            _announcer.Lapsed(code, JustLapsed);
        }
    }
    /// <summary>
    /// Expires anything whose window closed, and tells those requesters it lapsed (R-1.3c).
    /// </summary>
    /// <remarks>
    /// Expiring and announcing are one step here because they were two in the coordinator and the
    /// pair had to be remembered together. A lapse nobody is told about is the indefinite wait
    /// R-1.3c exists to end.
    /// </remarks>
    public void ExpireLapsed(DateTimeOffset now)
    {
        JustLapsed = Desk.ExpireLapsed(now);
        AnnounceLapsed();
    }

    /// <summary>Forgets everything, for the end of a session.</summary>
    public void Clear()
    {
        Audience.Clear();
        Desk.Clear();
        JustLapsed = Array.Empty<PendingAdmission>();
    }
}
