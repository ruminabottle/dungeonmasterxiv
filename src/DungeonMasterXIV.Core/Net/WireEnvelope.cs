using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// One message on the wire. Construct with the factory methods, which are what enforce that each
/// message type carries the fields it should and nothing it should not.
/// </summary>
/// <remarks>
/// <para>
/// The session code travels in the clear because the relay routes by it and cannot do its one job
/// otherwise. That is the deliberate disclosure R-1.9 requires the UI to state plainly: the relay
/// knows a connection exists, roughly when and how much, and where from. It does not know what was
/// said, because a <see cref="WireMessageType.SessionPayload"/> can only be built from a
/// <see cref="SealedPayload"/>, and the only way to obtain one of those is
/// <see cref="SessionCipher.Seal"/>.
/// </para>
/// <para>
/// No socket is opened here and no connection is made. This chunk defines the format; carrying it
/// belongs to the relay and client work.
/// </para>
/// </remarks>
public sealed record WireEnvelope
{
    private WireEnvelope(WireMessageType type, string sessionCode)
    {
        Type = type;
        SessionCode = sessionCode;
    }

    /// <summary>What this message is.</summary>
    public WireMessageType Type { get; private init; }

    /// <summary>The session this message belongs to, unhyphenated. Readable by the relay.</summary>
    public string SessionCode { get; private init; }

    /// <summary>Per-message nonce; present on <see cref="WireMessageType.SessionPayload"/> only.</summary>
    public byte[]? Nonce { get; private init; }

    /// <summary>Ciphertext; present on <see cref="WireMessageType.SessionPayload"/> only.</summary>
    public byte[]? Payload { get; private init; }

    /// <summary>SPKI public key; present on <see cref="WireMessageType.JoinRequest"/> only (D-11).</summary>
    public byte[]? PublicKey { get; private init; }

    /// <summary>Host asks the relay to claim <paramref name="code"/>.</summary>
    public static WireEnvelope ForCodeRequest(SessionCode code) =>
        new(WireMessageType.CodeRequest, code.Value);

    /// <summary>Relay grants the code.</summary>
    public static WireEnvelope ForCodeAccepted(SessionCode code) =>
        new(WireMessageType.CodeAccepted, code.Value);

    /// <summary>
    /// Relay refuses the code because it is already live. Carries no reason string: the only reason
    /// is "taken", and the host's response is to regenerate and retry regardless.
    /// </summary>
    public static WireEnvelope ForCodeRefused(SessionCode code) =>
        new(WireMessageType.CodeRefused, code.Value);

    /// <summary>Joiner asks to be admitted, presenting its ephemeral public key (D-11).</summary>
    public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return new WireEnvelope(WireMessageType.JoinRequest, code.Value) { PublicKey = publicKey };
    }

    /// <summary>
    /// Carries an encrypted payload between members. Takes a <see cref="SealedPayload"/> and not
    /// bytes, so there is no overload that would accept plaintext.
    /// </summary>
    public static WireEnvelope ForSessionPayload(SessionCode code, SealedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new WireEnvelope(WireMessageType.SessionPayload, code.Value)
        {
            Nonce = payload.Nonce,
            Payload = payload.Ciphertext,
        };
    }

    /// <summary>
    /// Rebuilds an envelope parsed from the wire. Internal because it is the one path that can
    /// produce a payload envelope without going through <see cref="SessionCipher"/>, and it exists
    /// only for <see cref="EnvelopeCodec"/>: bytes arriving from a relay are already whatever they
    /// are, and refusing to represent them would just move the problem.
    /// </summary>
    internal static WireEnvelope FromWire(
        WireMessageType type,
        string sessionCode,
        byte[]? nonce,
        byte[]? payload,
        byte[]? publicKey) =>
        new(type, sessionCode) { Nonce = nonce, Payload = payload, PublicKey = publicKey };

    /// <summary>
    /// Recovers the sealed payload from a received envelope, or null if this is not a payload
    /// message or arrived without the fields one needs.
    /// </summary>
    public SealedPayload? TryGetSealedPayload() =>
        Type == WireMessageType.SessionPayload && Nonce is not null && Payload is not null
            ? SealedPayload.FromWire(Nonce, Payload)
            : null;
}
