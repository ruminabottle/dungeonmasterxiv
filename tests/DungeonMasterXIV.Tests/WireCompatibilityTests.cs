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
