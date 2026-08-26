using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class WireEnvelopeTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    private static readonly byte[] Aad =
        WireEnvelope.AssociatedDataFor(Code, WireMessageType.SessionPayload);

    // A-1.5f, the format half. Fails if: the cipher is a pass-through, or the envelope carries a
    // payload in the clear.
    //
    // The payload field must be checked after base64 decoding, not by scanning the raw JSON. An
    // earlier version of this test only scanned the raw bytes and PASSED against a deliberately
    // substituted null cipher — base64 hid the plaintext from the byte search, so the test could
    // not detect the thing it exists to detect. Both checks are kept: the decoded one is the real
    // assertion, the raw scan catches a plaintext field appearing somewhere else in the envelope.
    [Fact]
    public void TheBytesThatLeaveAMemberDoNotContainThePlaintext()
    {
        var secret = Encoding.UTF8.GetBytes("Ysera drops to 4 hit points");
        var key = RandomNumberGenerator.GetBytes(SessionCipher.KeySize);

        var wire = EnvelopeCodec.Encode(
            WireEnvelope.ForSessionPayload(Code, SessionCipher.Seal(key, secret, Aad)));

        Assert.True(EnvelopeCodec.TryDecode(wire, out var decoded));
        Assert.NotNull(decoded!.Payload);
        Assert.False(ByteSequence.Contains(decoded.Payload!, secret), "Plaintext found in the payload field.");
        Assert.False(ByteSequence.Contains(wire, secret), "Plaintext found elsewhere in the encoded envelope.");
    }

    // Fails if: encoding or decoding drops a field. A payload that survives the relay but loses its
    // nonce cannot be decrypted by anyone.
    [Fact]
    public void ASessionPayloadSurvivesEncodingAndDecoding()
    {
        var key = RandomNumberGenerator.GetBytes(SessionCipher.KeySize);
        var plaintext = Encoding.UTF8.GetBytes("turn order: 21, 17, 9");
        var original = WireEnvelope.ForSessionPayload(Code, SessionCipher.Seal(key, plaintext, Aad));

        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(original), out var decoded));
        var recovered = decoded!.TryGetSealedPayload();

        Assert.NotNull(recovered);
        Assert.Equal(plaintext, SessionCipher.Open(key, recovered!, Aad));
    }

    // Fails if: the code-claim exchange R-1.2a describes cannot be expressed on the wire. Without
    // these three the host has no way to be refused and no way to retry.
    [Theory]
    [InlineData(WireMessageType.CodeRequest)]
    [InlineData(WireMessageType.CodeAccepted)]
    [InlineData(WireMessageType.CodeRefused)]
    public void EachStepOfTheCodeClaimExchangeRoundTrips(WireMessageType type)
    {
        var original = type switch
        {
            WireMessageType.CodeRequest => WireEnvelope.ForCodeRequest(Code),
            WireMessageType.CodeAccepted => WireEnvelope.ForCodeAccepted(Code),
            _ => WireEnvelope.ForCodeRefused(Code),
        };

        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(original), out var decoded));
        Assert.Equal(type, decoded!.Type);
        Assert.Equal(Code.Value, decoded.SessionCode);
    }

    // Fails if: a relay-control message starts carrying payload fields. The relay reads these, so
    // anything in them is disclosed by construction.
    [Fact]
    public void RelayControlMessagesCarryNoPayloadAndNoKey()
    {
        var request = WireEnvelope.ForCodeRequest(Code);

        Assert.Null(request.Payload);
        Assert.Null(request.Nonce);
        Assert.Null(request.PublicKey);
    }

    // Fails if: the join request loses the public key. D-11 puts the key exchange on this message,
    // so a join without one leaves nothing to derive a session key from.
    [Fact]
    public void AJoinRequestCarriesTheEphemeralPublicKeyIntactThroughTheWire()
    {
        using var joiner = new SessionKeyExchange();
        var original = WireEnvelope.ForJoinRequest(Code, joiner.PublicKey);

        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(original), out var decoded));
        Assert.Equal(joiner.PublicKey, decoded!.PublicKey);
    }

    // The test for finding 1. Fails if: the tag stops covering the envelope metadata — that is, if
    // Seal/Open go back to the 4-argument AES-GCM overload with no associated data.
    //
    // A relay forwards ciphertext it cannot read, but it can still alter what sits beside it. Here
    // it re-emits a payload sealed for one session stamped with another session code. Without the
    // binding the tag still verifies, because it never covered the code; with it, the receiver
    // recomputes a different AAD and the open fails.
    [Fact]
    public void APayloadRestampedWithAnotherSessionCodeWillNotOpen()
    {
        var key = RandomNumberGenerator.GetBytes(SessionCipher.KeySize);
        var elsewhere = SessionCode.FromValid("CFGH23");
        var payload = SessionCipher.Seal(key, Encoding.UTF8.GetBytes("the DM is at 3 hit points"), Aad);

        var reframed = WireEnvelope.ForSessionPayload(elsewhere, payload);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => SessionCipher.Open(key, reframed.TryGetSealedPayload()!, reframed.AssociatedData()));
    }

    // Also finding 1, the other half. Fails if: the message type stops being covered by the tag.
    // Today TryGetSealedPayload happens to refuse a non-payload type, so this would fail closed by
    // luck; the binding is what makes it fail closed by construction, and C2 adds a sender field
    // that will need the same protection.
    [Fact]
    public void APayloadRelabelledAsAnotherMessageTypeWillNotOpen()
    {
        var key = RandomNumberGenerator.GetBytes(SessionCipher.KeySize);
        var payload = SessionCipher.Seal(key, Encoding.UTF8.GetBytes("roll for initiative"), Aad);

        var relabelled = WireEnvelope.AssociatedDataFor(Code, WireMessageType.JoinRequest);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => SessionCipher.Open(key, payload, relabelled));
    }

    // Fails if: TryGetSealedPayload starts inventing payloads for messages that have none.
    [Fact]
    public void AControlMessageYieldsNoSealedPayload()
    {
        Assert.Null(WireEnvelope.ForCodeRefused(Code).TryGetSealedPayload());
    }

    // Fails if: the decoder throws instead of reporting failure. Anything at all can arrive from a
    // relay or a peer, and a malformed frame must not take the client down.
    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"Type\":")]
    public void MalformedBytesAreRejectedWithoutThrowing(string garbage)
    {
        Assert.False(EnvelopeCodec.TryDecode(Encoding.UTF8.GetBytes(garbage), out var decoded));
        Assert.Null(decoded);
    }
}
