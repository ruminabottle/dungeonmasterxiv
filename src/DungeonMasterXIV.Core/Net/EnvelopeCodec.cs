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

        if (wire?.SessionCode is null)
        {
            return false;
        }

        // D-14: a receiver ignores what it does not recognise, and that ignoring is a property of
        // THIS method rather than something each handler remembers — otherwise it is inconsistent by
        // construction. An unrecognised type becomes Unknown and decoding still succeeds, so an old
        // plugin survives a newer relay instead of refusing it. Rejecting here would be the opposite
        // of what D-14 asks for.
        var type = Enum.IsDefined(wire.Type) ? wire.Type : WireMessageType.Unknown;

        envelope = WireEnvelope.FromWire(
            type,
            wire.SessionCode,
            wire.Nonce,
            wire.Payload,
            wire.PublicKey,
            wire.HostPublicKey,
            wire.DeadlineUtcTicks);
        return true;
    }

    /// <summary>
    /// The serialised shape, kept separate from <see cref="WireEnvelope"/> so that the envelope can
    /// keep a locked-down construction path. A serializer needs a type it can freely populate; the
    /// envelope is a type that deliberately cannot be.
    /// </summary>
    private sealed class WireShape
    {
        public WireMessageType Type { get; set; }

        public string? SessionCode { get; set; }

        public byte[]? Nonce { get; set; }

        public byte[]? Payload { get; set; }

        public byte[]? PublicKey { get; set; }

        public byte[]? HostPublicKey { get; set; }

        public long? DeadlineUtcTicks { get; set; }
    }
}
