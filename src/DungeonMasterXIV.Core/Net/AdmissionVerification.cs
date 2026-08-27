namespace DungeonMasterXIV.Net;

/// <summary>
/// Whether the DM actually compared the fingerprint before admitting (R-1.3a).
/// </summary>
/// <remarks>
/// <para>
/// R-1.3a permits a DM to admit without comparing — we do not block a session on a step some groups
/// will skip — <b>but that admission is recorded and shown as unverified</b>, and no copy may
/// describe such a session as protected against interception.
/// </para>
/// <para>
/// This is a recorded fact rather than a UI flag because the claim it governs is a security claim: a
/// displayed-but-unconfirmed fingerprint is decorative, and calling it a defence is the overclaim
/// R-1.7a's forbidden-phrasing list exists to prevent (D-8).
/// </para>
/// </remarks>
public enum AdmissionVerification
{
    /// <summary>Admitted without the fingerprint being compared. The session is not MITM-protected.</summary>
    NotCompared = 0,

    /// <summary>The DM confirmed the fingerprint matched what the joiner read out of band.</summary>
    Confirmed = 1,
}
