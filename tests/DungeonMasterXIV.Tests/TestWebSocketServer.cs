using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A real WebSocket server, for tests that need an actual socket rather than a fake transport.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing about the protocol is written here.</b> <see cref="HttpListener"/> performs the
/// upgrade handshake and <see cref="WebSocket"/> does the framing — this type only starts a
/// listener and moves bytes.
/// </para>
/// <para>
/// The first version of this file hand-rolled the handshake, and its <c>Sec-WebSocket-Accept</c>
/// computation was wrong: the client refused every connection. Kept as a note because the failure
/// is the point — a test harness that reimplements a protocol can be wrong in the same direction as
/// nothing else, and then it is the harness under test rather than the code. Checked against RFC
/// 6455's published example, which is how the error was found rather than guessed at.
/// </para>
/// <para>
/// This is not the relay. It proves the plugin's transport speaks WebSocket to something real;
/// whether the relay behaves is the relay's own suite's job.
/// </para>
/// </remarks>
internal sealed class TestWebSocketServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource<WebSocket> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TestWebSocketServer()
    {
        Port = FreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = AcceptAsync();
    }

    /// <summary>The ephemeral port this server bound to.</summary>
    public int Port { get; }

    /// <summary>Where to point a client. Loopback, so <c>ws://</c> is permitted.</summary>
    public Uri Address => new($"ws://127.0.0.1:{Port}/session");

    /// <summary>Frames this server received, in arrival order.</summary>
    public BlockingCollection<byte[]> Received { get; } = new();

    /// <summary>Completes when the client has connected.</summary>
    public Task<WebSocket> Connected => _connected.Task;

    /// <summary>Sends one frame to the connected client.</summary>
    public async Task SendAsync(byte[] frame)
    {
        var socket = await Connected.ConfigureAwait(false);
        await socket.SendAsync(frame, WebSocketMessageType.Binary, true, _lifetime.Token)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        _lifetime.Dispose();
        Received.Dispose();
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task AcceptAsync()
    {
        try
        {
            var context = await _listener.GetContextAsync().ConfigureAwait(false);
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }

            var accepted = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
            _connected.TrySetResult(accepted.WebSocket);
            await ReceiveLoopAsync(accepted.WebSocket).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _connected.TrySetException(exception);
        }
    }

    private async Task ReceiveLoopAsync(WebSocket socket)
    {
        var buffer = new byte[8192];

        while (socket.State == WebSocketState.Open && !_lifetime.IsCancellationRequested)
        {
            using var frame = new MemoryStream();
            ValueWebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(), _lifetime.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                frame.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            Received.Add(frame.ToArray());
        }
    }
}
