namespace DungeonMasterXIV.Net;

/// <summary>
/// The serialised shape of an envelope: every field a peer might have sent, freely settable.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from <see cref="WireEnvelope"/> because the two want opposite things. A serializer
/// needs a type it can populate field by field; the envelope is deliberately a type that cannot be,
/// so that no caller can assemble one that the factories would refuse — <c>ForSessionPayload</c>
/// takes a <see cref="SealedPayload"/> precisely so no overload accepts plaintext.
/// </para>
/// <para>
/// <b>Internal, and that is the guard.</b> Only <see cref="EnvelopeCodec"/> can obtain one, so
/// nothing outside this assembly gains the ability to build an arbitrary envelope by going around
/// the factories.
/// </para>
/// <para>
/// It carries the fields as properties rather than as arguments to a factory because the field list
/// grows: <see cref="WireEnvelope.FromWire"/> had reached seven parameters, past the standards'
/// blocking limit of six, and one more optional field would have made that worse rather than
/// better. A new optional field is a property here and changes no signature.
/// </para>
/// </remarks>
internal sealed class WireShape
{
    /// <summary>What this message claims to be. Unrecognised values become <see cref="WireMessageType.Unknown"/>.</summary>
    public WireMessageType Type { get; set; }

    /// <summary>The routing key, validated as a session code before an envelope is built.</summary>
    public string? SessionCode { get; set; }

    /// <summary>Per-message nonce.</summary>
    public byte[]? Nonce { get; set; }

    /// <summary>Ciphertext.</summary>
    public byte[]? Payload { get; set; }

    /// <summary>The joining client's SPKI public key.</summary>
    public byte[]? PublicKey { get; set; }

    /// <summary>The host's SPKI public key.</summary>
    public byte[]? HostPublicKey { get; set; }

    /// <summary>When the admission window closes, as UTC ticks.</summary>
    public long? DeadlineUtcTicks { get; set; }

    /// <summary>
    /// The joiner's self-declared display name (R-1.3e). Optional and untrusted: a peer that omits
    /// it is simply not naming itself, and older peers that never heard of it decode unchanged.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The participant a returning client claims to be, if it claims one (R-1.5). Optional in the
    /// strongest sense: a peer that omits it is not making a claim, and older peers that have never
    /// heard of it decode unchanged.
    /// </summary>
    public string? ClaimedParticipantId { get; set; }

    /// <summary>
    /// The participant the HOST created for this joiner, on an acceptance (R-1.5c). Optional in the
    /// same sense as everything above: a host that omits it created none, and older peers that have
    /// never heard of it decode unchanged.
    /// </summary>
    /// <remarks>
    /// <b>A DIFFERENT FACT FROM <see cref="ClaimedParticipantId"/>, which is why it is a different
    /// field.</b> That one travels joiner to host and is an unauthenticated CLAIM; this one travels
    /// host to joiner and is the host's own ANSWER. One field serving both would make the direction
    /// the only thing distinguishing a claim from a record, and direction is not carried on the wire.
    /// </remarks>
    public string? ParticipantId { get; set; }
}
