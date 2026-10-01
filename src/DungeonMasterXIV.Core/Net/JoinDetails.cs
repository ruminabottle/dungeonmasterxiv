using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Net;

/// <summary>What a joiner tells the host, and what the host tells it back, sealed under their shared key.</summary>
public sealed record JoinDetails
{
    public string? DisplayName { get; init; }

    public string? ParticipantId { get; init; }
}

/// <summary>Seals join details for one message type and opens them again, refusing anything that does not authenticate.</summary>
public static class JoinDetailsCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static SealedPayload Seal(byte[] key, JoinDetails details, SessionCode code, WireMessageType type)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(details);

        return SessionCipher.Seal(
            key,
            JsonSerializer.SerializeToUtf8Bytes(details, Options),
            WireEnvelope.AssociatedDataFor(code, type));
    }

    public static JoinDetails? TryOpen(byte[] key, WireEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Nonce is null || envelope.Payload is null || envelope.Nonce.Length != SessionCipher.NonceSize)
        {
            return null;
        }

        try
        {
            var plaintext = SessionCipher.Open(
                key, SealedPayload.FromWire(envelope.Nonce, envelope.Payload), envelope.AssociatedData());
            return JsonSerializer.Deserialize<JoinDetails>(plaintext, Options);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
