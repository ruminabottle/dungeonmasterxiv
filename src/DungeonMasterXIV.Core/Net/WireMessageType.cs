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
}
