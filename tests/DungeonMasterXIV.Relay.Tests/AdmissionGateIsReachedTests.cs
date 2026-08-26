using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// R-1.3b's gate, asserted over a real socket: an unadmitted connection is routed nothing and can
/// originate nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Correct is not the same as reached.</b> <see cref="RelayRouterTests"/> proves the gating rule
/// is right by calling the router directly, and a rule that no receive path invokes would pass
/// exactly those tests while the relay forwarded to everyone — the suite would look wired because
/// tests are callers. So this drives the gate the way a client does: through the WebSocket, into
/// the endpoint, through the hub. If someone deletes the check from the receive path, the router
/// tests still pass and these fail.
/// </para>
/// <para>
/// Nothing here is arranged through the registry, deliberately. This is the one place that would be
/// worthless if it were.
/// </para>
/// </remarks>
public sealed class AdmissionGateIsReachedTests
{
    /// <summary>Long enough that a real forward would have landed, short enough not to stall a run.</summary>
    private static readonly TimeSpan LongEnoughToHaveArrived = TimeSpan.FromMilliseconds(750);

    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    [Fact]
    public async Task APendingJoinerReceivesNoSessionTrafficAtAll()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();
        using var joiner = await relay.ConnectAsync();
        using var hostKeys = new SessionKeyExchange();
        using var joinerKeys = new SessionKeyExchange();

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));
        await RelayUnderTest.ReceiveAsync(host);

        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForJoinRequest(Code, joinerKeys.PublicKey));
        var (request, _) = await RelayUnderTest.ReceiveAsync(host);
        Assert.Equal(WireMessageType.JoinRequest, request.Type);

        // The DM has not decided. Everything the host says now must not reach this connection —
        // including ciphertext it could not read, because a count and a cadence are inference D-13
        // forbids just as squarely as readable content.
        var key = hostKeys.DeriveSharedKey(joinerKeys.PublicKey, Code);
        var payload = SessionCipher.Seal(
            key,
            "a roll nobody outside the session may even count"u8.ToArray(),
            WireEnvelope.AssociatedDataFor(Code, WireMessageType.SessionPayload));

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForSessionPayload(Code, payload));

        Assert.Null(await RelayUnderTest.TryReceiveAsync(joiner, LongEnoughToHaveArrived));
    }

    [Fact]
    public async Task APendingJoinerCannotPushAnythingIntoTheSession()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();
        using var joiner = await relay.ConnectAsync();
        using var joinerKeys = new SessionKeyExchange();

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));
        await RelayUnderTest.ReceiveAsync(host);

        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForJoinRequest(Code, joinerKeys.PublicKey));
        await RelayUnderTest.ReceiveAsync(host);

        var payload = SealedPayload.FromWire(new byte[SessionCipher.NonceSize], [1, 2, 3, 4]);
        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForSessionPayload(Code, payload));

        Assert.Null(await RelayUnderTest.TryReceiveAsync(host, LongEnoughToHaveArrived));
    }

    /// <summary>
    /// A connection that never asked to join is not in the session at all, so a code it happens to
    /// know buys it nothing. Admission is the security model, not the code (R-1.7).
    /// </summary>
    [Fact]
    public async Task AStrangerHoldingTheCodeReceivesNothing()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();
        using var stranger = await relay.ConnectAsync();
        using var hostKeys = new SessionKeyExchange();
        using var otherKeys = new SessionKeyExchange();

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));
        await RelayUnderTest.ReceiveAsync(host);

        var key = hostKeys.DeriveSharedKey(otherKeys.PublicKey, Code);
        var payload = SessionCipher.Seal(
            key,
            "session traffic"u8.ToArray(),
            WireEnvelope.AssociatedDataFor(Code, WireMessageType.SessionPayload));

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForSessionPayload(Code, payload));

        Assert.Null(await RelayUnderTest.TryReceiveAsync(stranger, LongEnoughToHaveArrived));
    }
}
