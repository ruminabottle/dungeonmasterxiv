using System;
using System.Text;

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

    /// <summary>
    /// The <b>joining client's</b> SPKI public key. Present on
    /// <see cref="WireMessageType.JoinRequest"/>, and echoed on
    /// <see cref="WireMessageType.JoinAccepted"/> so the joiner can tell which request was answered.
    /// Its meaning never changes: it is always the joiner's key (D-14).
    /// </summary>
    public byte[]? PublicKey { get; private init; }

    /// <summary>
    /// The <b>host's</b> SPKI public key, on <see cref="WireMessageType.JoinAccepted"/>.
    /// </summary>
    /// <remarks>
    /// A separate field rather than reusing <see cref="PublicKey"/>, because a field that means the
    /// joiner's key on one message and the host's on another is repurposing — which D-14 forbids —
    /// and because the ambiguity is the whole defect: without the host's key an admitted joiner is
    /// routed and permanently unable to decrypt anything, which reads as an encryption bug.
    /// </remarks>
    public byte[]? HostPublicKey { get; private init; }

    /// <summary>
    /// When the admission window closes, as UTC ticks, on
    /// <see cref="WireMessageType.JoinRequest"/> acknowledgements. Decided once by the DM's client;
    /// see <see cref="AdmissionDeadline"/>.
    /// </summary>
    public long? DeadlineUtcTicks { get; private init; }

    /// <summary>
    /// The participant a returning client claims to be, on
    /// <see cref="WireMessageType.JoinRequest"/> (R-1.5). Null means no claim was made.
    /// </summary>
    /// <remarks>
    /// <b>A claim, never a credential.</b> It is unauthenticated text from the joining client, so
    /// nothing may be granted on the strength of it. Its entire effect is to let the host look the
    /// participant up and change what the DM's prompt <i>says</i>; the DM still approves every
    /// relink, every session (R-1.5, D-8). Resolving it is <c>CampaignRelink</c>'s job (it lives in the campaign layer, which reads this field but is not referenced from here), and
    /// what the prompt shows is derived from what resolved rather than from what was claimed.
    /// </remarks>
    public string? ClaimedParticipantId { get; private init; }

    /// <summary>
    /// The envelope metadata a payload is bound to: the session it belongs to and what kind of
    /// message it is. Authenticated by <see cref="SessionCipher"/> but never transmitted — the
    /// receiver rebuilds it from the envelope in front of it, so a re-framed payload fails its tag
    /// check rather than decrypting under a type or session code it was never sealed for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The encoding is unambiguous because the separator cannot occur in either part: a session code
    /// is drawn from an alphabet with no colon, and the type is rendered as digits. That is what is
    /// actually relied on, and it holds for <see cref="AssociatedData"/> as well as for this method
    /// — <see cref="EnvelopeCodec.TryDecode"/> rejects an envelope whose code is not a session code,
    /// so the instance method cannot be reached with an arbitrary wire string.
    /// </para>
    /// <para>
    /// The earlier wording justified this by the code being exactly
    /// <see cref="SessionCode.Length"/> characters, which was true of this method and false of
    /// <see cref="AssociatedData"/>. Fixed-length would also stop being the reason the moment a
    /// second string field joined the binding, and a reason that is false of a path it covers is
    /// worse than none: the next reader checks it where it holds and extends the pattern.
    /// </para>
    /// </remarks>
    public static byte[] AssociatedDataFor(SessionCode code, WireMessageType type) =>
        Encoding.UTF8.GetBytes($"{code.Value}:{(int)type}");

    /// <summary>The binding for this envelope, for a receiver about to open its payload.</summary>
    public byte[] AssociatedData() =>
        Encoding.UTF8.GetBytes($"{SessionCode}:{(int)Type}");

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
    /// The same join request, stamped by the DM's client with the instant its window closes. Only
    /// the host decides this; a joining client is told (R-1.3c).
    /// </summary>
    public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey, AdmissionDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return new WireEnvelope(WireMessageType.JoinRequest, code.Value)
        {
            PublicKey = publicKey,
            DeadlineUtcTicks = deadline.UtcTicks,
        };
    }

    /// <summary>
    /// A join request from a returning client, claiming a participant it believes is its own (R-1.5).
    /// </summary>
    /// <remarks>
    /// The claim is carried, not trusted. A host that does not recognise it simply shows an ordinary
    /// join prompt, and a host that does recognise it still shows a prompt the DM must answer.
    /// </remarks>
    /// <param name="code">The session being joined.</param>
    /// <param name="publicKey">The joiner's ephemeral public key (D-11).</param>
    /// <param name="claimedParticipantId">The participant UUID this client believes is its own.</param>
    public static WireEnvelope ForRelinkRequest(SessionCode code, byte[] publicKey, Guid claimedParticipantId)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return new WireEnvelope(WireMessageType.JoinRequest, code.Value)
        {
            PublicKey = publicKey,
            ClaimedParticipantId = claimedParticipantId.ToString("D"),
        };
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
    internal static WireEnvelope FromWire(WireMessageType type, string sessionCode, WireShape wire) =>
        new(type, sessionCode)
        {
            Nonce = wire.Nonce,
            Payload = wire.Payload,
            PublicKey = wire.PublicKey,
            HostPublicKey = wire.HostPublicKey,
            DeadlineUtcTicks = wire.DeadlineUtcTicks,
            ClaimedParticipantId = wire.ClaimedParticipantId,
        };

    /// <summary>
    /// Host admits a joiner. Carries <b>two</b> keys: the joiner's, echoed so they know which
    /// request this answers, and the host's, without which the joiner can derive no shared key.
    /// </summary>
    public static WireEnvelope ForJoinAccepted(SessionCode code, byte[] joinerPublicKey, byte[] hostPublicKey)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        ArgumentNullException.ThrowIfNull(hostPublicKey);
        return new WireEnvelope(WireMessageType.JoinAccepted, code.Value)
        {
            PublicKey = joinerPublicKey,
            HostPublicKey = hostPublicKey,
        };
    }

    /// <summary>Host refuses a joiner (R-1.3b). Carries no key, because nothing follows.</summary>
    public static WireEnvelope ForJoinDenied(SessionCode code, byte[] joinerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        return new WireEnvelope(WireMessageType.JoinDenied, code.Value) { PublicKey = joinerPublicKey };
    }

    /// <summary>The window closed unanswered (R-1.3c). Distinct from a denial.</summary>
    public static WireEnvelope ForJoinLapsed(SessionCode code, byte[] joinerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        return new WireEnvelope(WireMessageType.JoinLapsed, code.Value) { PublicKey = joinerPublicKey };
    }

    /// <summary>
    /// The admission outcome this envelope expresses, or null if it is not an admission answer.
    /// Consumers go through <see cref="AdmissionOutcome.Match{T}"/>, so none can drop a case.
    /// </summary>
    public AdmissionOutcome? TryGetAdmissionOutcome() => Type switch
    {
        WireMessageType.JoinAccepted when HostPublicKey is not null => AdmissionOutcome.Accepted(HostPublicKey),
        WireMessageType.JoinDenied => AdmissionOutcome.Denied(),
        WireMessageType.JoinLapsed => AdmissionOutcome.Lapsed(),
        _ => null,
    };

    /// <summary>The admission deadline carried here, if any.</summary>
    public AdmissionDeadline? TryGetDeadline() =>
        DeadlineUtcTicks is { } ticks ? AdmissionDeadline.TryFromWire(ticks) : null;

    /// <summary>
    /// Recovers the sealed payload from a received envelope, or null if this is not a payload
    /// message or arrived without the fields one needs.
    /// </summary>
    public SealedPayload? TryGetSealedPayload() =>
        Type == WireMessageType.SessionPayload && Nonce is not null && Payload is not null
            ? SealedPayload.FromWire(Nonce, Payload)
            : null;
}
