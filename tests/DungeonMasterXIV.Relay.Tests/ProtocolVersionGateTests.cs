using System.Net;
using System.Net.WebSockets;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// A-1.5i: a client whose protocol version differs from the relay's is refused at connect, with the
/// mismatch named and never a partial connection (R-1.7b).
/// </summary>
/// <remarks>
/// Driven over real sockets against the shipped relay. The refusal is observed the way the plugin
/// observes it — a failed connect carrying a status and a header — rather than by calling the gate,
/// because a gate that is correct and never reached is an ungated relay with passing tests.
/// </remarks>
public sealed class ProtocolVersionGateTests
{
    [Fact]
    public async Task AMatchingVersionConnects()
    {
        using var sandbox = new RelaySandbox();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.ContentRoot);

        using var client = await relay.ConnectStatingAsync($"{ProtocolVersion.Current}");

        Assert.Equal(WebSocketState.Open, client.State);
    }

    [Theory]
    [InlineData("2")]      // a relay older than the client
    [InlineData("99")]     // a client from far in the future
    [InlineData(null)]     // a build predating this requirement, which states nothing
    [InlineData("not-a-number")]
    public async Task AMismatchedVersionIsRefusedBeforeAnySocketExists(string? stated)
    {
        using var sandbox = new RelaySandbox();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.ContentRoot);

        await Assert.ThrowsAnyAsync<WebSocketException>(() => relay.ConnectStatingAsync(stated));

        var refused = relay.RefusedSocket;
        Assert.NotNull(refused);

        // Never a partial connection: there is no open socket to be partway into.
        Assert.NotEqual(WebSocketState.Open, refused!.State);
        Assert.Equal(HttpStatusCode.UpgradeRequired, refused.HttpStatusCode);
    }

    /// <summary>
    /// The refusal names the relay's own version, which is what lets the client say which side is
    /// behind rather than reporting a generic failure.
    /// </summary>
    [Fact]
    public async Task TheRefusalStatesTheRelaysOwnVersion()
    {
        using var sandbox = new RelaySandbox();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.ContentRoot);

        await Assert.ThrowsAnyAsync<WebSocketException>(() => relay.ConnectStatingAsync("99"));

        var headers = relay.RefusedSocket!.HttpResponseHeaders;
        Assert.NotNull(headers);
        Assert.True(headers!.TryGetValue(ProtocolVersion.Header, out var stated));
        Assert.Equal($"{ProtocolVersion.Current}", stated.Single());
    }

    /// <summary>
    /// A refused client is not in the relay's session bookkeeping at all — it never became a
    /// connection, so there is nothing for it to be counted in or routed into.
    /// </summary>
    [Fact]
    public async Task ARefusedClientNeverBecomesAConnection()
    {
        using var sandbox = new RelaySandbox();
        await using var relay = await RelayUnderTest.StartAsync(sandbox.ContentRoot);

        await Assert.ThrowsAnyAsync<WebSocketException>(() => relay.ConnectStatingAsync("99"));

        Assert.Equal(0, relay.Registry.LiveSessionCount);

        // And the relay is still serving: a refusal is not a shutdown.
        using var good = await relay.ConnectAsync();
        await RelayUnderTest.SendAsync(good, WireEnvelope.ForCodeRequest(SessionCode.FromValid("BCDFGH")));
        var (accepted, _) = await RelayUnderTest.ReceiveAsync(good);

        Assert.Equal(WireMessageType.CodeAccepted, accepted.Type);
    }
}
