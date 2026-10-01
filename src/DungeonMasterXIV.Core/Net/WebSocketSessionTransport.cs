using System;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DungeonMasterXIV.Net;

/// <summary>A WebSocket connection to the relay that sends and receives binary envelope frames.</summary>
public sealed class WebSocketSessionTransport : ISessionTransport, IDisposable
{
    private readonly ISessionTransportLog _log;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _lifetime;
    private bool _connecting;

    private volatile ClientWebSocket? _connected;

    public WebSocketSessionTransport(ISessionTransportLog log) => _log = log;

    public event Action<SessionFailure>? Failed;

    public event Action<byte[]>? Received;

    public bool IsConnected => _connecting || _socket?.State == WebSocketState.Open;

    public bool IsReadyToSend =>
        ReferenceEquals(_connected, _socket) && _socket?.State == WebSocketState.Open;

    public void Connect(Uri relay)
    {
        ArgumentNullException.ThrowIfNull(relay);
        Disconnect();

        var lifetime = new CancellationTokenSource();
        var socket = new ClientWebSocket();
        _lifetime = lifetime;
        _socket = socket;
        _connecting = true;

        _socket.Options.KeepAliveInterval = TransportContract.KeepAliveInterval;
        _socket.Options.KeepAliveTimeout = TransportContract.KeepAliveTimeout;

        _socket.Options.CollectHttpResponseDetails = true;

        _log.Information("Connecting to the configured session relay.");

        _ = ConnectAsync(socket, ProtocolVersion.AppendTo(relay), lifetime.Token);
    }

    public void Disconnect()
    {
        _connecting = false;
        if (_lifetime is not null)
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetime = null;
        }

        if (_socket is null)
        {
            return;
        }

        var socket = _socket;
        _socket = null;
        _connected = null;

        CloseThenDispose(socket);
        _log.Information("Session relay connection closed.");
    }

    public void Send(byte[] envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (_socket is not { State: WebSocketState.Open } socket || _lifetime is null)
        {
            return;
        }

        _ = socket.SendAsync(envelope, WebSocketMessageType.Binary, endOfMessage: true, _lifetime.Token);
    }

    public void Dispose() => Disconnect();

    private void CloseThenDispose(ClientWebSocket socket)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            socket.Dispose();
            return;
        }

        var failure = TransportShutdown.CloseThenDispose(
            token => socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, token),
            socket.Dispose,
            TransportShutdown.CloseTimeout);

        if (failure is not null)
        {
            _log.Warning(failure, "The session relay connection was disposed without a completed close handshake.");
        }
    }

    private static SessionFailure ClassifyRefusal(ClientWebSocket socket)
    {
        var stated = socket.HttpResponseHeaders is not null
            && socket.HttpResponseHeaders.TryGetValue(ProtocolVersion.Header, out var values)
                ? values.FirstOrDefault()
                : null;

        return ProtocolVersion.ClassifyRefusal(
            socket.HttpStatusCode == HttpStatusCode.UpgradeRequired,
            stated);
    }

    private async Task ConnectAsync(ClientWebSocket socket, Uri relay, CancellationToken token)
    {
        try
        {
            await socket.ConnectAsync(relay, token).ConfigureAwait(false);

            _connected = socket;

            await ReceiveLoopAsync(socket, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is WebSocketException
                                              or ObjectDisposedException
                                              or InvalidOperationException)
        {
            var failure = ClassifyRefusal(socket);
            _log.Warning(
                exception,
                failure == SessionFailure.RelayUnreachable
                    ? "Could not reach the session relay."
                    : "The session relay refused this build's protocol version.");

            Failed?.Invoke(failure);
        }
        finally
        {
            if (ReferenceEquals(socket, _socket))
            {
                _connecting = false;
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[8192];

        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            using var frame = new MemoryStream();
            ValueWebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(), token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Failed?.Invoke(SessionFailure.ConnectionLost);
                    return;
                }

                frame.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            Received?.Invoke(frame.ToArray());
        }
    }
}
