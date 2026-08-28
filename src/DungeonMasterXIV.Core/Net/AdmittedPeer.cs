using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>
/// A participant the DM has admitted. The only thing session state can be addressed to.
/// </summary>
/// <remarks>
/// The constructor is <c>internal</c>, so this is <b>structurally</b> unconstructible from the plugin
/// project and the test project, and <b>conventionally</b> unconstructible within Core itself, where
/// payload-building code lives alongside it. That is the accurate statement of the guarantee and it
/// is deliberately not "obtainable only from <see cref="SessionAudience.Admit"/>", which would claim
/// more than <c>internal</c> delivers.
/// <para>
/// Either way it is D-13's None level made structural at the boundary that matters: a client at None
/// is absent from the payload because there is nothing to put in one, rather than being filtered out
/// of a payload built for everyone.
/// </para>
/// <para>
/// This is a <c>class</c> on purpose. As a <c>struct</c>, <c>default(AdmittedPeer)</c> would be a
/// valid-looking peer with a null code and the guarantee would be gone.
/// </para>
/// </remarks>
public sealed class AdmittedPeer
{
    private readonly byte[]? _publicKey;

    internal AdmittedPeer(
        PeerCode peerCode,
        SessionRole role,
        AdmissionVerification verification,
        byte[]? publicKey = null,
        DisplayName displayName = default)
    {
        PeerCode = peerCode;
        Role = role;
        Verification = verification;
        _publicKey = publicKey?.ToArray();
        DisplayName = displayName;
    }

    /// <summary>
    /// The key this participant presented, kept so the host can seal content TO them (D-11).
    /// </summary>
    /// <remarks>
    /// <b>Why the host must keep it.</b> Keys are pairwise: <c>DeriveSharedKey</c> gives the host a
    /// different shared secret with every participant, so there is no one key that reaches the room.
    /// Without the peer's public key here, an admitted participant is addressable by the relay and
    /// unreachable by the host — the session could route to them and never say anything they could
    /// open.
    /// <para>
    /// <b>Copied in and copied out, because these bytes are now load-bearing for a seal (D-11).</b>
    /// An array handed straight through is one the caller can still mutate, and after this chunk a
    /// mutation would change the key a roster is sealed with rather than merely corrupting a record.
    /// <see cref="SessionAudience.Recipients"/> already returns a read-only list; the elements were
    /// the remaining hole.
    /// </para>
    /// <para>
    /// <b>Copying rather than a <c>SealTo</c> method, deliberately.</b> This type is a record of what
    /// is known about a participant. Giving it a sealing method would hand a data type a dependency
    /// on the cipher and the session code, and put a crypto operation in the one place that gets
    /// passed around freely — the copy keeps the boundary where the key lives, which is the smaller
    /// surface.
    /// </para>
    /// </remarks>
    public byte[]? PublicKey => _publicKey?.ToArray();

    /// <summary>What this participant calls themselves (R-1.3e). A label, never an identity.</summary>
    /// <remarks>
    /// Carried so the roster has something to show. Two participants may hold the same value
    /// (A-1.2d); <see cref="PeerCode"/> is what tells them apart, here exactly as in the prompt.
    /// </remarks>
    public DisplayName DisplayName { get; }

    /// <summary>
    /// The session-scoped code identifying this participant. Never a character name — R-1.3 requires
    /// the DM's prompt to identify a requester by code, and D-8 forbids the name reaching a log, a
    /// file or an export.
    /// </summary>
    /// <remarks>
    /// A <see cref="Net.PeerCode"/> rather than a <c>string</c>: this is the identity two
    /// participants with the same <see cref="DisplayName"/> are told apart by (A-1.2d), so a value
    /// this product could not have generated must not be able to reach it.
    /// </remarks>
    public PeerCode PeerCode { get; }

    /// <summary>
    /// What this participant may do (E-11). An Assistant runs the table; only the DM controls who is
    /// at it, which is why admission does not branch on this.
    /// </summary>
    public SessionRole Role { get; }

    /// <summary>
    /// Whether the DM compared the fingerprint before admitting (R-1.3a). Recorded rather than
    /// inferred, because "we did not check" and "we checked and it matched" are different facts and
    /// the UI must not present the first as the second.
    /// </summary>
    public AdmissionVerification Verification { get; }
}
