using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Turns a <see cref="WireEnvelope"/> into bytes and back.
/// </summary>
/// <remarks>
/// JSON via the BCL serializer rather than a bespoke byte layout. A framing format invented here
/// would be one more thing to get subtly wrong, and D-11's instruction to avoid hand-rolling
/// applies most sharply next to the cryptography. Byte arrays travel as base64.
/// </remarks>
public static class EnvelopeCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serialises an envelope for transmission.</summary>
    public static byte[] Encode(WireEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var wire = new WireShape
        {
            Type = envelope.Type,
            SessionCode = envelope.SessionCode,
            Nonce = envelope.Nonce,
            Payload = envelope.Payload,
            PublicKey = envelope.PublicKey,
            HostPublicKey = envelope.HostPublicKey,
            DeadlineUtcTicks = envelope.DeadlineUtcTicks,
            DisplayName = envelope.DisplayName,
            ClaimedParticipantId = envelope.ClaimedParticipantId,
            ParticipantId = envelope.ParticipantId,
        };

        return JsonSerializer.SerializeToUtf8Bytes(wire, Options);
    }

    /// <summary>
    /// Parses bytes received from the wire.
    /// </summary>
    /// <remarks>
    /// Reports whether the bytes were well-formed and nothing more. Anything can arrive from a peer
    /// or a relay, so every judgement about whether to trust the contents is the caller's, and a
    /// payload is not believed until <see cref="SessionCipher.Open"/> authenticates it.
    /// </remarks>
    public static bool TryDecode(byte[] bytes, out WireEnvelope? envelope)
    {
        envelope = null;
        if (bytes is null || bytes.Length == 0)
        {
            return false;
        }

        WireShape? wire;
        try
        {
            wire = JsonSerializer.Deserialize<WireShape>(bytes, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        // Rejecting a code that is not a session code is what makes the routing key trustworthy for
        // everything downstream — including the associated-data binding, whose unambiguity argument
        // depends on the code containing no separator. Unlike an unknown message TYPE, which D-14
        // requires be tolerated, a malformed routing key is not a message from the future: nothing
        // can be done with it and no later version makes it meaningful.
        if (wire?.SessionCode is null || !SessionCode.TryParse(wire.SessionCode, out _))
        {
            return false;
        }

        // D-14: a receiver ignores what it does not recognise, and that ignoring is a property of
        // THIS method rather than something each handler remembers — otherwise it is inconsistent by
        // construction. An unrecognised type becomes Unknown and decoding still succeeds, so an old
        // plugin survives a newer relay instead of refusing it. Rejecting here would be the opposite
        // of what D-14 asks for.
        var type = Enum.IsDefined(wire.Type) ? wire.Type : WireMessageType.Unknown;

        envelope = WireEnvelope.FromWire(type, wire.SessionCode, wire);
        return true;
    }

}
