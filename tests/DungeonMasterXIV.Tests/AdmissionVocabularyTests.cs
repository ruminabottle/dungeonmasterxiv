using System;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class AdmissionVocabularyTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // The defect this chunk exists to prevent, and the one the consumer found rather than the
    // author. Fails if: acceptance carries only one key. Without the HOST's key the joiner is
    // admitted, routed, and permanently unable to derive anything — which presents as an encryption
    // bug rather than a missing field, so it would be looked for in the wrong place.
    //
    // Asserted by actually deriving and decrypting, not by checking the field is non-null: a field
    // holding the wrong key would pass that and fail this.
    [Fact]
    public void AnAdmittedJoinerCanDeriveTheSessionKeyFromWhatTheAcceptanceCarries()
    {
        using var host = new SessionKeyExchange();
        using var joiner = new SessionKeyExchange();
        var plaintext = new byte[] { 4, 8, 15, 16, 23, 42 };
        var aad = WireEnvelope.AssociatedDataFor(Code, WireMessageType.SessionPayload);

        var acceptance = WireEnvelope.ForJoinAccepted(Code, joiner.PublicKey, host.PublicKey);
        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(acceptance), out var received));

        var joinerKey = received!.TryGetAdmissionOutcome()!
            .Match(hostKey => joiner.DeriveSharedKey(hostKey, Code), () => null!, () => null!);
        var hostKey = host.DeriveSharedKey(joiner.PublicKey, Code);

        Assert.Equal(plaintext, SessionCipher.Open(joinerKey, SessionCipher.Seal(hostKey, plaintext, aad), aad));
    }

    // Fails if: acceptance stops echoing the joiner's key. With several requests outstanding the
    // joiner would have no way to tell which one was answered.
    [Fact]
    public void AcceptanceEchoesTheJoinersKeySoTheyKnowWhichRequestWasAnswered()
    {
        using var host = new SessionKeyExchange();
        using var joiner = new SessionKeyExchange();

        var acceptance = WireEnvelope.ForJoinAccepted(Code, joiner.PublicKey, host.PublicKey);
        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(acceptance), out var received));

        Assert.Equal(joiner.PublicKey, received!.PublicKey);
        Assert.Equal(host.PublicKey, received.HostPublicKey);
        Assert.NotEqual(received.PublicKey, received.HostPublicKey);
    }

    // Fails if: denial and lapse collapse into one state. The distinction is behavioural — a lapsed
    // player may reasonably ask again, a denied one should not be invited to — so a consumer must be
    // able to tell them apart from the wire alone.
    [Fact]
    public void DenialAndLapseAreDistinctOnTheWire()
    {
        using var joiner = new SessionKeyExchange();

        var denied = WireEnvelope.ForJoinDenied(Code, joiner.PublicKey);
        var lapsed = WireEnvelope.ForJoinLapsed(Code, joiner.PublicKey);

        Assert.NotEqual(denied.Type, lapsed.Type);
        Assert.Equal("denied", denied.TryGetAdmissionOutcome()!.Match(_ => "accepted", () => "denied", () => "lapsed"));
        Assert.Equal("lapsed", lapsed.TryGetAdmissionOutcome()!.Match(_ => "accepted", () => "denied", () => "lapsed"));
    }

    // Fails if: an admission answer stops surviving encoding. A denial that does not arrive is
    // silence, which R-1.3b forbids by name.
    [Theory]
    [InlineData(WireMessageType.JoinDenied)]
    [InlineData(WireMessageType.JoinLapsed)]
    public void AnAdmissionAnswerSurvivesTheWire(WireMessageType type)
    {
        using var joiner = new SessionKeyExchange();
        var original = type == WireMessageType.JoinDenied
            ? WireEnvelope.ForJoinDenied(Code, joiner.PublicKey)
            : WireEnvelope.ForJoinLapsed(Code, joiner.PublicKey);

        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(original), out var received));

        Assert.Equal(type, received!.Type);
        Assert.NotNull(received.TryGetAdmissionOutcome());
    }

    // Fails if: a message that is not an admission answer starts producing one.
    [Fact]
    public void AMessageThatIsNotAnAdmissionAnswerYieldsNoOutcome()
    {
        Assert.Null(WireEnvelope.ForCodeRequest(Code).TryGetAdmissionOutcome());
    }

    // Fails if: acceptance without the host's key is treated as a usable acceptance. A malformed
    // acceptance must not become an outcome the joiner acts on — it has no key to derive with.
    [Fact]
    public void AnAcceptanceMissingTheHostKeyIsNotAUsableOutcome()
    {
        using var joiner = new SessionKeyExchange();
        var malformed = EnvelopeCodec.Encode(WireEnvelope.ForJoinRequest(Code, joiner.PublicKey));
        var tampered = System.Text.Encoding.UTF8.GetString(malformed).Replace("\"Type\":4", "\"Type\":6");

        Assert.True(EnvelopeCodec.TryDecode(System.Text.Encoding.UTF8.GetBytes(tampered), out var received));
        Assert.Equal(WireMessageType.JoinAccepted, received!.Type);
        Assert.Null(received.TryGetAdmissionOutcome());
    }
}
