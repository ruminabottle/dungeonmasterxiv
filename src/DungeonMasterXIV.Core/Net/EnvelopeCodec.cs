using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Net;

/// <summary>Encodes wire envelopes to JSON bytes and decodes them back, rejecting frames without a valid code.</summary>
public static class EnvelopeCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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

        if (wire?.SessionCode is null || !SessionCode.TryParse(wire.SessionCode, out _))
        {
            return false;
        }

        var type = Enum.IsDefined(wire.Type) ? wire.Type : WireMessageType.Unknown;

        envelope = WireEnvelope.FromWire(type, wire.SessionCode, wire);
        return true;
    }

}
