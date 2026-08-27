using System.Net.WebSockets;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// A connection holds a <b>set</b> of roles, and leaving unwinds every one of them.
/// </summary>
/// <remarks>
/// <para>
/// <c>SessionCoordinator</c> drives hosting and joining over one transport, so a DM who starts a
/// session and then joins someone else's is one connection that is a host in one session and a
/// joiner in another. That is an ordinary user, not an exotic case. The registry originally kept one
/// session per connection, and the second role silently overwrote the first.
/// </para>
/// <para>
/// Both sequences below are driven entirely over real sockets — claim, join, disconnect, admit — so
/// they reproduce what a user does rather than asserting a data structure. Where an assertion has to
/// query the registry it says so and why.
/// </para>
/// </remarks>
public sealed class ConnectionRolesAreASetTests
{
    private static readonly SessionCode Hosted = SessionCode.FromValid("BCDFGH");
    private static readonly SessionCode Other = SessionCode.FromValid("JKMNPR");

    /// <summary>
    /// Hosting one session and joining another, then leaving, must free the hosted code.
    /// </summary>
    /// <remarks>
    /// The defect this reproduces: registering as pending overwrote the connection's session, so
    /// disconnecting unwound the joined session and left the hosted one in the table forever with a
    /// dead host. The code was then unclaimable for the lifetime of the process, and the stranded
    /// session was memory that never evicted — invisible to the no-write test, because it is memory
    /// rather than a file.
    /// </remarks>
    [Fact]
    public async Task AHostWhoAlsoJoinsElsewhereFreesItsOwnCodeWhenItLeaves()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var otherHost = await relay.ConnectAsync();
        await ClaimAsync(otherHost, Other);

        var dungeonMaster = await relay.ConnectAsync();
        await ClaimAsync(dungeonMaster, Hosted);

        // The same connection now joins somebody else's session. Both roles are live at once.
        using var keys = new SessionKeyExchange();
        await RelayUnderTest.SendAsync(dungeonMaster, WireEnvelope.ForJoinRequest(Other, keys.PublicKey));
        var (request, _) = await RelayUnderTest.ReceiveAsync(otherHost);
        Assert.Equal(WireMessageType.JoinRequest, request.Type);

        await dungeonMaster.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        dungeonMaster.Dispose();

        using var newHost = await relay.ConnectAsync();
        Assert.True(
            await TryClaimEventuallyAsync(relay, newHost, Hosted),
            $"{Hosted.ToDisplayString()} is still held after its host disconnected, so the session was "
            + "never unwound and the code is stranded for the lifetime of the process.");
    }

    /// <summary>
    /// The other session survives. Unwinding one role must not tear down a session the connection
    /// was merely a guest in.
    /// </summary>
    [Fact]
    public async Task LeavingDoesNotEndASessionTheConnectionOnlyJoined()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var otherHost = await relay.ConnectAsync();
        await ClaimAsync(otherHost, Other);

        var dungeonMaster = await relay.ConnectAsync();
        await ClaimAsync(dungeonMaster, Hosted);

        using var guestKeys = new SessionKeyExchange();
        await RelayUnderTest.SendAsync(dungeonMaster, WireEnvelope.ForJoinRequest(Other, guestKeys.PublicKey));
        await RelayUnderTest.ReceiveAsync(otherHost);

        await dungeonMaster.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        dungeonMaster.Dispose();

        // If the other session had been torn down too, this join would be refused as not-live.
        using var latecomer = await relay.ConnectAsync();
        using var latecomerKeys = new SessionKeyExchange();
        await RelayUnderTest.SendAsync(latecomer, WireEnvelope.ForJoinRequest(Other, latecomerKeys.PublicKey));

        var (stillAlive, _) = await RelayUnderTest.ReceiveAsync(otherHost);
        Assert.Equal(WireMessageType.JoinRequest, stillAlive.Type);
    }

    /// <summary>
    /// A connection pending under two keys is not admittable under either after it leaves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retrying a join with a fresh ephemeral key is the ordinary thing to do after a lapse
    /// (R-1.3c), so one connection outstanding under two keys is normal. Forgetting only the first
    /// left the second admittable after the connection had gone, and admitting it put a dead id into
    /// the member set where nothing ever removed it.
    /// </para>
    /// <para>
    /// The sequence is driven over sockets. The assertion queries the registry because the
    /// consequence of this defect is <b>memory rather than traffic</b> — a dead member id is never
    /// delivered to, so nothing observable at the socket distinguishes it. That is the same reason
    /// the no-write test could not have caught it, and it is stated rather than glossed.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AJoinerPendingUnderTwoKeysLeavesNoDeadMemberBehind()
    {
        using var sandbox = new TemporaryDirectory();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.Path);

        using var host = await relay.ConnectAsync();
        await ClaimAsync(host, Hosted);

        var joiner = await relay.ConnectAsync();
        using var firstAttempt = new SessionKeyExchange();
        using var secondAttempt = new SessionKeyExchange();

        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForJoinRequest(Hosted, firstAttempt.PublicKey));
        await RelayUnderTest.ReceiveAsync(host);
        await RelayUnderTest.SendAsync(joiner, WireEnvelope.ForJoinRequest(Hosted, secondAttempt.PublicKey));
        await RelayUnderTest.ReceiveAsync(host);

        await joiner.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        joiner.Dispose();

        // The host answers both requests after the joiner has gone — a DM clicking accept on a
        // prompt for somebody who just closed their game.
        await RelayUnderTest.SendAsync(
            host,
            WireEnvelope.ForJoinAccepted(Hosted, firstAttempt.PublicKey, firstAttempt.PublicKey));
        await RelayUnderTest.SendAsync(
            host,
            WireEnvelope.ForJoinAccepted(Hosted, secondAttempt.PublicKey, secondAttempt.PublicKey));

        await WaitForRelayToSettleAsync(relay, Hosted);

        var everyone = relay.Registry.MembersExcept(Hosted.Value, "nobody");
        Assert.True(
            everyone.Count == 1,
            "Only the host should remain. A departed joiner was admitted anyway and left a dead id in "
            + $"the member set, which nothing removes: [{string.Join(", ", everyone)}]");
    }

    private static async Task ClaimAsync(WebSocket connection, SessionCode code)
    {
        await RelayUnderTest.SendAsync(connection, WireEnvelope.ForCodeRequest(code));
        var (accepted, _) = await RelayUnderTest.ReceiveAsync(connection);
        Assert.Equal(WireMessageType.CodeAccepted, accepted.Type);
    }

    /// <summary>
    /// Retries a claim until the relay has finished unwinding the previous holder. Polls over the
    /// socket rather than waiting a fixed time, so a slow machine does not turn a pass into a flake
    /// and a genuine leak still fails.
    /// </summary>
    private static async Task<bool> TryClaimEventuallyAsync(
        RelayUnderTest relay,
        WebSocket connection,
        SessionCode code)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await RelayUnderTest.SendAsync(connection, WireEnvelope.ForCodeRequest(code));
            var (answer, _) = await RelayUnderTest.ReceiveAsync(connection);

            if (answer.Type == WireMessageType.CodeAccepted)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    /// <summary>
    /// Waits for the relay to have processed a disconnect. Synchronisation, not assertion — the
    /// test's claim is made below this, over what the registry then holds.
    /// </summary>
    private static async Task WaitForRelayToSettleAsync(RelayUnderTest relay, SessionCode code)
    {
        for (var attempt = 0; attempt < 40 && relay.Registry.MembersExcept(code.Value, "nobody").Count > 1; attempt++)
        {
            await Task.Delay(50);
        }
    }
}
