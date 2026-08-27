namespace DungeonMasterXIV.Net;

/// <summary>
/// What a <see cref="WireEnvelope"/> carries.
/// </summary>
/// <remarks>
/// Two classes of message, and the difference decides what the relay can read. The first three are
/// addressed to the relay itself, so the relay must be able to read them. <see cref="SessionPayload"/>
/// is addressed to session members and reaches the relay as ciphertext (R-1.9, A-1.5f).
/// </remarks>
public enum WireMessageType
{
    /// <summary>
    /// A type this build does not recognise. Never sent — <see cref="EnvelopeCodec"/> maps any
    /// unrecognised value to this on receipt, so D-14's "ignore what you do not recognise" is a
    /// property of the deserializer rather than something each handler remembers.
    /// </summary>
    Unknown = 0,

    /// <summary>Host to relay: claim this code. R-1.2a — the host proposes, the relay arbitrates.</summary>
    CodeRequest = 1,

    /// <summary>Relay to host: the code is yours.</summary>
    CodeAccepted = 2,

    /// <summary>Relay to host: already in use. The host regenerates and retries (R-1.2a).</summary>
    CodeRefused = 3,

    /// <summary>Joiner to host, via relay: asking to be admitted, carrying an ephemeral public key (D-11).</summary>
    JoinRequest = 4,

    /// <summary>Member to member: end-to-end encrypted. The relay forwards it and cannot read it.</summary>
    SessionPayload = 5,

    /// <summary>
    /// Host to joiner: admitted. Carries the <b>host's</b> public key, which is the half the joiner
    /// cannot obtain any other way — see <see cref="WireEnvelope.ForJoinAccepted"/>.
    /// </summary>
    JoinAccepted = 6,

    /// <summary>Host to joiner: refused. Somebody looked and said no (R-1.3b).</summary>
    JoinDenied = 7,

    /// <summary>
    /// Host to joiner: the window closed with no answer. Distinct from
    /// <see cref="JoinDenied"/> on purpose — nobody looked, so asking again is reasonable (R-1.3c).
    /// </summary>
    JoinLapsed = 8,

    /// <summary>
    /// Host to joiner: your request is in front of the DM, here is the host's public key, and here
    /// is when the window closes. Carries no decision — the DM has not made one yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This message exists because of when it is sent, not what it contains (D-11 amended).</b>
    /// <see cref="JoinAccepted"/> already carries the host's key, and carrying it there is too late:
    /// the joiner cannot compute the fingerprint until the DM has already decided, so the check
    /// meant to inform the decision arrives after it. A joiner who is only ever sent
    /// <see cref="JoinAccepted"/> has nothing to compare and cannot detect a substituted host key.
    /// </para>
    /// <para>
    /// <b>A new type rather than a field on <see cref="JoinRequest"/>.</b> D-14 permits either, but
    /// <see cref="JoinRequest"/> means "joiner to host, asking to be admitted"; putting the host's
    /// key on it would make one type mean two opposite things depending on direction, and D-14 says
    /// no message type ever changes meaning. An old client decodes this as
    /// <see cref="Unknown"/> and ignores it, which is the interoperability D-14 is for.
    /// </para>
    /// </remarks>
    JoinPending = 9,
}
