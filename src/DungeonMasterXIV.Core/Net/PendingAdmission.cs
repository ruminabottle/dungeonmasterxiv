using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A join request the DM has not answered yet: who is asking, the fingerprint to compare, and when
/// the window closes.
/// </summary>
/// <remarks>
/// <para>
/// The requester is identified by their <b>session-scoped code, never a character name</b> (R-1.3,
/// D-8). The fingerprint is the one value computed from both keys (A-1.3f), so the DM and the joiner
/// are reading the same string off their two screens.
/// </para>
/// <para>
/// <b>Confirmation is a deliberate act and cannot be defaulted into.</b> R-1.3a forbids a pre-ticked
/// box, so <see cref="FingerprintConfirmed"/> starts false and only
/// <see cref="ConfirmFingerprintMatched"/> sets it — there is no constructor parameter and no setter
/// that could be initialised the wrong way. A DM may still admit without confirming; what they cannot
/// do is have it recorded as confirmed without having said so.
/// </para>
/// </remarks>
public sealed class PendingAdmission
{
    /// <param name="peerCode">The requester's session-scoped code. Never a character name.</param>
    /// <param name="fingerprint">The combined fingerprint, rendered per R-1.3a.</param>
    /// <param name="deadline">When the window closes. Decided by the DM's client and carried in C6's vocabulary.</param>
    /// <param name="isRelink">Whether this is a returning participant asking to relink (R-1.5).</param>
    /// <param name="joinerPublicKey">The key they presented, echoed on acceptance (D-11).</param>
    public PendingAdmission(
        string peerCode,
        string fingerprint,
        AdmissionDeadline deadline,
        bool isRelink = false,
        byte[]? joinerPublicKey = null)
    {
        PeerCode = peerCode;
        Fingerprint = fingerprint;
        Deadline = deadline;
        IsRelink = isRelink;
        JoinerPublicKey = joinerPublicKey;
    }

    /// <summary>The requester's session-scoped code.</summary>
    public string PeerCode { get; }

    /// <summary>The fingerprint both parties compare out of band.</summary>
    public string Fingerprint { get; }

    /// <summary>When this request lapses.</summary>
    public AdmissionDeadline Deadline { get; }

    /// <summary>
    /// Whether this is a returning participant relinking to a character in a known campaign (R-1.5).
    /// The DM approves every relink, every session — it is never silent and never automatic (D-8).
    /// </summary>
    public bool IsRelink { get; }

    /// <summary>
    /// The key this participant presented with their request (D-11). Carried because acceptance must
    /// echo it — with several requests outstanding it is how the joiner tells which one was answered
    /// — and because the fingerprint is computed from it together with the host's.
    /// </summary>
    public byte[]? JoinerPublicKey { get; }

    /// <summary>Whether the DM has said the fingerprint matched. Starts false, always.</summary>
    public bool FingerprintConfirmed { get; private set; }

    /// <summary>How this admission will be recorded if accepted now.</summary>
    public AdmissionVerification Verification =>
        FingerprintConfirmed ? AdmissionVerification.Confirmed : AdmissionVerification.NotCompared;

    /// <summary>
    /// The DM states that the fingerprint on their screen matches what the joiner read to them
    /// <b>out of band</b> — voice, Discord, whatever the group already uses. It cannot be carried in
    /// the plugin, because a channel an attacker controls cannot verify that attacker.
    /// </summary>
    public void ConfirmFingerprintMatched() => FingerprintConfirmed = true;

    /// <summary>How long the requester has left, for the countdown R-1.3c requires while it runs.</summary>
    public TimeSpan RemainingAt(DateTimeOffset now) => Deadline.RemainingAt(now);

    /// <summary>Whether the window has closed unanswered.</summary>
    public bool HasLapsedAt(DateTimeOffset now) => Deadline.HasLapsedAt(now);
}
