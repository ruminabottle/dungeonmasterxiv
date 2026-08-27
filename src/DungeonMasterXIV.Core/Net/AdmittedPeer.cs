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
    internal AdmittedPeer(string peerCode, SessionRole role, AdmissionVerification verification)
    {
        PeerCode = peerCode;
        Role = role;
        Verification = verification;
    }

    /// <summary>
    /// The session-scoped code identifying this participant. Never a character name — R-1.3 requires
    /// the DM's prompt to identify a requester by code, and D-8 forbids the name reaching a log, a
    /// file or an export.
    /// </summary>
    public string PeerCode { get; }

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
