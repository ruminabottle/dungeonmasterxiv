using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Who may receive session state, and the only route by which anything is addressed to them.
/// </summary>
/// <remarks>
/// <para>
/// D-13 assigns a per-user access level to anything one participant can see and another might not.
/// The levels this chunk carries, and what each one gets:
/// </para>
/// <list type="bullet">
/// <item><b>None</b> — a client that has asked to join and not been admitted, and a client that was
/// denied or removed. It receives nothing: no roster, no state, no events, no count of anything
/// (R-1.3). It is not on <see cref="Recipients"/>, so nothing can be addressed to it.</item>
/// <item><b>Limited</b> — a pending joiner, with respect to the session itself. It learns the
/// session exists, because it typed a live code and was told it is waiting on a person; it learns
/// nothing about the contents. That disclosure is required by R-1.8 and is not a leak: R-1.2 makes
/// the code non-secret and admission the security model.</item>
/// <item><b>Observer</b> — an admitted participant. Receives session state; changes nothing about
/// who is in the session.</item>
/// <item><b>Owner</b> — the DM. Admits, removes and ends, and is the sole author of shared state
/// (D-3).</item>
/// </list>
/// <para>
/// The inference half of D-13 is why <see cref="Count"/> is not exposed to anyone but the host and
/// why nothing here derives a length, an index or an ordering that a non-admitted client could see.
/// Deleting a recipient from a list is half the task; the other half is that no number computed
/// from the full list ever reaches someone who is not on it.
/// </para>
/// </remarks>
public sealed class SessionAudience
{
    private readonly List<AdmittedPeer> _admitted = new();

    /// <summary>
    /// Everyone session state may be addressed to. Contains only admitted participants, so building
    /// a payload for this audience cannot include a client at None.
    /// </summary>
    /// <remarks>
    /// A genuine read-only wrapper, not the backing list typed as an interface. Returning
    /// <c>_admitted</c> directly would let a caller downcast to <see cref="List{T}"/> and mutate it,
    /// and would hand C2's receive loop a collection that can change under enumeration.
    /// </remarks>
    public IReadOnlyList<AdmittedPeer> Recipients => _admitted.AsReadOnly();

    /// <summary>
    /// How many participants are admitted. For the host's own display (R-1.1) and for nobody else —
    /// a count derived from the full set is exactly the inference D-13 forbids reaching a None
    /// client, so this must never be put on the wire to participants.
    /// </summary>
    public int Count => _admitted.Count;

    /// <summary>
    /// Admits a participant and returns the token that lets state be addressed to them.
    /// </summary>
    /// <remarks>
    /// Admitting the same participant twice returns the existing token rather than adding a second
    /// entry, so a retried admission cannot inflate the host's count or duplicate a recipient.
    /// </remarks>
    /// <param name="peerCode">The participant's session-scoped code.</param>
    /// <param name="role">What they may do (E-11). Defaults to a plain player.</param>
    /// <param name="verification">
    /// Whether the DM compared the fingerprint (R-1.3a). Defaults to <b>not compared</b>, so an
    /// admission is only ever recorded as verified when a caller says so explicitly.
    /// </param>
    /// <param name="publicKey">
    /// The key they presented, so the host can seal content to them (D-11). Optional because a test
    /// arranging an audience has no keys to hand; <b>the production path always supplies it</b>, and
    /// <c>AnAdmittedPeerKeepsTheKeyAndNameTheyArrivedWith</c> is what stops that being an assumption.
    /// </param>
    /// <param name="displayName">What they call themselves (R-1.3e), for the roster.</param>
    public AdmittedPeer Admit(
        PeerCode peerCode,
        SessionRole role = SessionRole.Player,
        AdmissionVerification verification = AdmissionVerification.NotCompared,
        byte[]? publicKey = null,
        DisplayName displayName = default)
    {
        var existing = _admitted.FirstOrDefault(peer => peer.PeerCode == peerCode);
        if (existing is not null)
        {
            return existing;
        }

        var peer = new AdmittedPeer(peerCode, role, verification, publicKey, displayName);
        _admitted.Add(peer);
        return peer;
    }

    /// <summary>
    /// Removes a participant. R-1.3: that client immediately stops receiving state, which here means
    /// it stops being addressable rather than starting to be filtered.
    /// </summary>
    public bool Remove(PeerCode peerCode)
    {
        var peer = _admitted.FirstOrDefault(candidate => candidate.PeerCode == peerCode);
        return peer is not null && _admitted.Remove(peer);
    }

    /// <summary>Whether this participant may receive session state.</summary>
    public bool IsAdmitted(PeerCode peerCode) => _admitted.Any(peer => peer.PeerCode == peerCode);

    /// <summary>The admitted participant with this code, or null.</summary>
    public AdmittedPeer? Find(PeerCode peerCode) =>
        _admitted.FirstOrDefault(peer => peer.PeerCode == peerCode);

    /// <summary>
    /// How many admitted participants had their fingerprint compared. For the host's own display:
    /// a session with unverified admissions is not protected against interception and the UI must
    /// not say otherwise (R-1.3a, D-8).
    /// </summary>
    public int ConfirmedCount =>
        _admitted.Count(peer => peer.Verification == AdmissionVerification.Confirmed);

    /// <summary>Drops every participant, for the end of a session.</summary>
    public void Clear() => _admitted.Clear();
}
