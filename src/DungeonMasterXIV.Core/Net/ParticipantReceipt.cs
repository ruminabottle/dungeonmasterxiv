using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Reads the participant a host has told this client it is, out of an admission envelope (R-1.5c).
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own type because these are PROTOCOL POLICY, not envelope shape</b> — the same seam
/// <c>CampaignRelink.Resolve</c> already sits on for the claim travelling the other way.
/// <see cref="WireEnvelope"/> says what arrived; this says what may be believed about it, and the
/// two answer to different requirements.
/// </para>
/// <para>
/// <b>Extracting it was forced, and saying so is more useful than pretending it was planned.</b>
/// The accessor and its reasoning put <see cref="WireEnvelope"/> at 411 lines against a 400 block —
/// margin -11, a hard breach. That is the seam the size limit exists to find, and the limit found a
/// real one rather than an arbitrary place to cut.
/// </para>
/// </remarks>
public static class ParticipantReceipt
{
    /// <summary>
    /// The participant this client has been told it is, or null if this envelope does not tell it
    /// one. <b>Three checks, all at this one door.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TYPE</b> — only an acceptance tells you this. R-1.3b: an unadmitted client receives no
    /// session traffic at all, so reading it off a pending notice would accept an identity before
    /// the decision that grants it.
    /// </para>
    /// <para>
    /// <b>ADDRESSEE — and the relay does NOT discharge this.</b> An honest relay forwards an
    /// acceptance to a single recipient, but <b>D-11 assumes an attacker may control the relay</b>,
    /// which is exactly the position from which one hands this client somebody else's acceptance.
    /// The UUID <i>is</i> the relink claim, so taking one addressed elsewhere is how a client comes
    /// to hold a credential it can present later and have the DM see a plausible returning player.
    /// <para>
    /// <b>THE ADMISSION IS NOW CHECKED TOO, AND THIS PARAGRAPH USED TO SAY IT WAS NOT.</b> While
    /// DMXENG-47 was in review this read <i>"what this does not fix"</i> — the admission accepted on
    /// any acceptance carrying a host key, whoever it named, so a joiner awaiting a decision was
    /// admitted by a stranger's acceptance and derived a session key. That was BUG-85, found by this
    /// feature's own two-joiner harness, fixed in
    /// <see cref="WireEnvelopeReading.TryGetAdmissionOutcome"/>, and merged before this.
    /// <b>Updated here because the change that falsifies a comment owns it</b>, and a stale
    /// <i>"this is not fixed"</i> sitting beside the fix is worse than the caveat was ever worth.
    /// </para>
    /// <para>
    /// <b>The two guards stay separate on purpose.</b> That one decides whether an ADMISSION
    /// happened; this decides whether a FIELD may be believed. Same comparison, different
    /// consequence — and folding this into that one would make a participant id something the
    /// admission grants rather than something the envelope carries.
    /// </para>
    /// </para>
    /// <para>
    /// <b>PARSE</b> — a host controls these characters and a value that is not a GUID is not a
    /// participant. Dropped here rather than carried inward as a string for something further in to
    /// fail on, which is BUG-56's lesson applied to a different field.
    /// </para>
    /// </remarks>
    /// <param name="envelope">What arrived.</param>
    /// <param name="ownPublicKey">This client's own public key. Null before one exists.</param>
    public static Guid? TryRead(WireEnvelope? envelope, byte[]? ownPublicKey) =>
        envelope is { Type: WireMessageType.JoinAccepted, PublicKey: { } addressee }
        && ownPublicKey is not null
        && CryptographicOperations.FixedTimeEquals(addressee, ownPublicKey)
        && Guid.TryParseExact(envelope.ParticipantId, "D", out var id)
            ? id
            : null;
}
