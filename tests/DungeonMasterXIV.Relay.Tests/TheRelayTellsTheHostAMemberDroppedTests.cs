using System.Net.WebSockets;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// A-1.28's relay half: when a member's connection goes away, the host is TOLD.
/// </summary>
/// <remarks>
/// <para>
/// <b>Driven over a real socket, because the whole point is that a connection went away.</b> A test
/// that called <c>DisconnectAsync</c> directly would prove the method sends something; it would not
/// prove anything CALLS it when a client actually vanishes, which is the half that did not exist
/// before this chunk.
/// </para>
/// <para>
/// <b>What this cannot show, stated rather than implied:</b> that no code path anywhere infers a
/// drop from silence. A-1.28 forbids that too and nothing structural reaches it — a seat clock
/// started by an absence of traffic would need no field any test here can see.
/// </para>
/// </remarks>
public sealed class TheRelayTellsTheHostAMemberDroppedTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // A-1.28. Fails on origin/main before this chunk: the relay observed the drop, wrote it to its
    // own log, and told nobody -- so a host could only have learned by inferring from silence, which
    // is what the criterion forbids.
    [Fact]
    public async Task TheHostIsToldWhenAMembersConnectionGoesAway()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);
        using var host = await relay.ConnectAsync();
        var joinerKey = await AdmitAJoinerAsync(relay, host);

        var (notice, _) = await RelayUnderTest.ReceiveAsync(host);

        Assert.Equal(WireMessageType.ConnectionDropped, notice.Type);
        Assert.Equal(Code.Value, notice.SessionCode);

        // THE KEY, and it is the only thing the relay says about who left. The host derives its own
        // peer code from it — the relay never names a participant, which is D-3 kept rather than
        // merely respected (A-1.29).
        Assert.Equal(joinerKey, notice.PublicKey);
    }

    // The forgery guard from the other side. A client-sent drop notice must not be laundered onward:
    // it would let any keyholder tell a host that somebody else vanished. RelayRouter drops it as
    // RelayOnlyMessageFromClient, alongside the relay's other own-answers.
    [Fact]
    public async Task AClientCannotForgeADropNotice()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);
        using var host = await relay.ConnectAsync();
        using var joiner = await relay.ConnectAsync();

        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));
        await RelayUnderTest.ReceiveAsync(host);

        using var keys = new SessionKeyExchange();
        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForConnectionDropped(Code, keys.PublicKey));

        Assert.Null(
            await RelayUnderTest.TryReceiveAsync(host, TimeSpan.FromMilliseconds(400)));
    }

    /// <summary>Admits a joiner over the wire, then closes its socket. Returns the key it used.</summary>
    private static async Task<byte[]> AdmitAJoinerAsync(RelayUnderTest relay, WebSocket host)
    {
        await RelayUnderTest.SendAsync(host, WireEnvelope.ForCodeRequest(Code));
        await RelayUnderTest.ReceiveAsync(host);

        using var joinerKeys = new SessionKeyExchange();
        using var hostKeys = new SessionKeyExchange();
        var joiner = await relay.ConnectAsync();

        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForJoinRequest(Code, joinerKeys.PublicKey));
        var (request, _) = await RelayUnderTest.ReceiveAsync(host);

        await RelayUnderTest.SendAsync(
            host, WireEnvelope.ForJoinAccepted(Code, request.PublicKey!, hostKeys.PublicKey));
        await RelayUnderTest.ReceiveAsync(joiner);

        // The vanishing. No close frame, no leave notice -- the ungraceful departure the relay is
        // the only party able to observe.
        joiner.Dispose();
        return joinerKeys.PublicKey;
    }
}
