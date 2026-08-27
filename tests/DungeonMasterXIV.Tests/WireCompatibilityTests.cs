using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// D-14: the wire format only grows. These are the tests that make that checkable rather than
/// aspirational — the contract already has a merged client and a consumer building against it.
/// </summary>
public class WireCompatibilityTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // Fails if: a message type is renumbered. The numbers are the contract — a relay and a plugin
    // that disagree about which integer means JoinDenied fail at runtime, silently, on the one path
    // nobody exercises until a DM refuses somebody.
    [Theory]
    [InlineData(WireMessageType.Unknown, 0)]
    [InlineData(WireMessageType.CodeRequest, 1)]
    [InlineData(WireMessageType.CodeAccepted, 2)]
    [InlineData(WireMessageType.CodeRefused, 3)]
    [InlineData(WireMessageType.JoinRequest, 4)]
    [InlineData(WireMessageType.SessionPayload, 5)]
    [InlineData(WireMessageType.JoinAccepted, 6)]
    [InlineData(WireMessageType.JoinDenied, 7)]
    [InlineData(WireMessageType.JoinLapsed, 8)]
    public void MessageTypeNumbersAreFixed(WireMessageType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    // D-14's core tolerance. Fails if: an unrecognised type is rejected rather than ignored — which
    // would mean a relay adding a message type breaks every plugin already installed, the precise
    // outcome D-14 exists to prevent. Note this asserts decoding SUCCEEDS: refusing would be the
    // opposite of what the directive asks for.
    [Fact]
    public void AMessageTypeFromTheFutureDecodesAsUnknownRatherThanBeingRejected()
    {
        var fromANewerRelay = Encoding.UTF8.GetBytes("{\"Type\":9999,\"SessionCode\":\"BKD7RM\"}");

        Assert.True(EnvelopeCodec.TryDecode(fromANewerRelay, out var received));

        Assert.Equal(WireMessageType.Unknown, received!.Type);
        Assert.Null(received.TryGetAdmissionOutcome());
    }

    // Fails if: unknown FIELDS start being rejected. A newer relay adding a field must not break an
    // older plugin, and D-14 says the ignoring is the deserializer's job rather than each handler's.
    [Fact]
    public void AFieldFromTheFutureIsIgnoredRatherThanRejected()
    {
        var fromANewerRelay = Encoding.UTF8.GetBytes(
            "{\"Type\":1,\"SessionCode\":\"BKD7RM\",\"SomethingAddedLater\":\"whatever\"}");

        Assert.True(EnvelopeCodec.TryDecode(fromANewerRelay, out var received));

        Assert.Equal(WireMessageType.CodeRequest, received!.Type);
    }

    // Finding 2's substance, and PR #4's finding 8 closed. Fails if: TryDecode stops validating the
    // routing key. A code that is not a session code is not a message from the future — D-14's
    // tolerance is for things a later version gives meaning to, and nothing ever makes a
    // ten-thousand-character routing key meaningful. It is also what the associated-data binding's
    // unambiguity argument rests on: the code must contain no separator, and only this check makes
    // that true of the instance method as well as the static one.
    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"NOTACODE\"")]
    [InlineData("\"AEIOUY\"")]
    [InlineData("\"BKD7RM:9\"")]
    public void AnEnvelopeWhoseRoutingKeyIsNotASessionCodeIsRejected(string code)
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"Type\":1,\"SessionCode\":{code}}}");

        Assert.False(EnvelopeCodec.TryDecode(bytes, out var received));
        Assert.Null(received);
    }

    // Fails if: validation is tightened past what a human may type. A code arrives on the wire
    // unhyphenated, and rejecting the hyphenated form a person pasted would break joining.
    [Fact]
    public void AValidRoutingKeyStillDecodes()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"Type\":1,\"SessionCode\":\"BKD7RM\"}");

        Assert.True(EnvelopeCodec.TryDecode(bytes, out var received));
        Assert.Equal("BKD7RM", received!.SessionCode);
    }

    // Fails if: a new optional field becomes required. An older sender omits it, and D-14 forbids
    // that being a decode failure.
    [Fact]
    public void AMessageWithoutTheNewOptionalFieldsStillDecodes()
    {
        var fromAnOlderPlugin = Encoding.UTF8.GetBytes("{\"Type\":4,\"SessionCode\":\"BKD7RM\"}");

        Assert.True(EnvelopeCodec.TryDecode(fromAnOlderPlugin, out var received));

        Assert.Null(received!.HostPublicKey);
        Assert.Null(received.TryGetDeadline());
    }
}
