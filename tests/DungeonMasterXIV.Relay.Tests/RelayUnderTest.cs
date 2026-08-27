using System.Net.WebSockets;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay;
using DungeonMasterXIV.Relay.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// A running relay, started the way the shipped one starts, on a loopback port with TLS off.
/// </summary>
/// <remarks>
/// <para>
/// It calls <see cref="RelayApp.Build"/> — the same method <c>Program.cs</c> calls — rather than
/// assembling a host that resembles it. A-1.5e is a claim about the relay we ship, so a harness
/// that built its own pipeline would prove something about the harness.
/// </para>
/// <para>
/// TLS is off here and only here. That is not what production does (see
/// <see cref="RelayOptions.UseTls"/>, and D-2 on why a proxy may not terminate it instead); it is
/// off because a loopback test that had to mint a certificate would be testing certificate
/// handling, and the routing and retention behaviour under test is identical either way.
/// </para>
/// </remarks>
public sealed class RelayUnderTest : IAsyncDisposable
{
    private readonly WebApplication _app;

    private RelayUnderTest(WebApplication app, int port)
    {
        _app = app;
        Port = port;
    }

    /// <summary>The loopback port this instance bound.</summary>
    public int Port { get; }

    /// <summary>
    /// The running relay's own session bookkeeping.
    /// </summary>
    /// <remarks>
    /// <b>Never used to arrange anything.</b> C6 merged, so admission now travels as a real message
    /// and every session in this suite is driven end to end over sockets. This is exposed only for
    /// the one assertion that cannot be made at the socket: a leaked member id is memory, never
    /// delivered to, so nothing observable on the wire distinguishes it from an absent one. See
    /// <see cref="ConnectionRolesAreASetTests"/>, which says so at the point it does this.
    /// </remarks>
    public SessionRegistry Registry => _app.Services.GetRequiredService<SessionRegistry>();

    /// <summary>Starts a relay whose content root is <paramref name="contentRoot"/>.</summary>
    public static async Task<RelayUnderTest> StartAsync(string contentRoot)
    {
        var app = RelayApp.Build(new RelayOptions
        {
            Port = 0,
            UseTls = false,
            ContentRoot = contentRoot,
        });

        await app.StartAsync();
        return new RelayUnderTest(app, RelayApp.BoundPort(app));
    }

    /// <summary>Opens a client WebSocket to this relay.</summary>
    public async Task<ClientWebSocket> ConnectAsync()
    {
        var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{Port}/relay"), CancellationToken.None);
        return client;
    }

    /// <summary>Sends one envelope as a single binary message, matching the framing contract.</summary>
    public static Task SendAsync(WebSocket socket, WireEnvelope envelope) =>
        socket.SendAsync(
            EnvelopeCodec.Encode(envelope),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            CancellationToken.None);

    /// <summary>
    /// Sends bytes exactly as given, so a test can put on the wire something the wire types would
    /// refuse to build — a message type from a client newer than this relay, for instance.
    /// </summary>
    public static Task SendRawAsync(WebSocket socket, byte[] bytes) =>
        socket.SendAsync(bytes, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);

    /// <summary>
    /// Receives one envelope, or fails the wait. Returns the raw bytes too, because the ciphertext
    /// assertions need what actually crossed the wire and not a re-encoding of it.
    /// </summary>
    public static async Task<(WireEnvelope Envelope, byte[] Bytes)> ReceiveAsync(
        WebSocket socket,
        TimeSpan? timeout = null)
    {
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        var buffer = new byte[64 * 1024];

        var result = await socket.ReceiveAsync(buffer, deadline.Token);
        var bytes = buffer[..result.Count];

        Assert.True(EnvelopeCodec.TryDecode(bytes, out var envelope), "Relay sent bytes that are not an envelope.");
        return (envelope!, bytes);
    }

    /// <summary>
    /// Waits for a message, returning null if none arrives. Used where the point is that nothing
    /// should arrive; the timeout is the assertion, so it is short enough not to stall a suite and
    /// long enough that a real forward would have landed.
    /// </summary>
    /// <remarks>
    /// <b>This ABORTS the socket, so it must be the last thing done with it.</b> Cancelling a
    /// WebSocket receive aborts the connection in .NET rather than merely abandoning the wait, so a
    /// socket that has been through here cannot be reused — and a test that tried would fail with
    /// an invalid-state error that reads exactly like the relay having hung up, which is the wrong
    /// diagnosis. Where a test needs to prove silence AND then keep using the connection, assert on
    /// what the next message turns out to be instead.
    /// </remarks>
    public static async Task<byte[]?> TryReceiveAsync(WebSocket socket, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        var buffer = new byte[64 * 1024];

        try
        {
            var result = await socket.ReceiveAsync(buffer, deadline.Token);
            return buffer[..result.Count];
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
