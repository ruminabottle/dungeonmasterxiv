using System.Net.WebSockets;
using DungeonMasterXIV.Relay.Diagnostics;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>
/// Accepts a WebSocket and pumps complete messages into <see cref="RelayHub"/> until it closes.
/// </summary>
/// <remarks>
/// The only file in the relay that knows the transport is WebSocket. Everything the relay decides
/// happens behind <see cref="RelayHub"/> against <see cref="IRelayConnection"/>, so a change of
/// framing lands here and nowhere else.
/// </remarks>
public sealed class WebSocketRelayEndpoint(RelayHub hub, ConnectionDirectory directory, RelayLog log, RelayOptions options)
{
    private readonly RelayHub _hub = hub;
    private readonly ConnectionDirectory _directory = directory;
    private readonly RelayLog _log = log;
    private readonly RelayOptions _options = options;

    /// <summary>Serves one accepted WebSocket for its lifetime.</summary>
    public async Task ServeAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var connectionId = Guid.NewGuid().ToString("n");
        await using var connection = new WebSocketRelayConnection(connectionId, socket, _options.OutboundQueueCapacity);

        _directory.Add(connection);
        _log.ConnectionOpened(connectionId);

        var reason = "closed by peer";
        try
        {
            await PumpAsync(socket, connection, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            reason = "relay shutting down";
        }
        catch (WebSocketException exception)
        {
            reason = $"transport error: {exception.WebSocketErrorCode}";
        }
        catch (Exception exception)
        {
            reason = "faulted";
            _log.ConnectionFaulted(connectionId, exception);
        }
        finally
        {
            // Still in a finally: an ungracefully dropped peer must unwind exactly like a polite one,
            // and a connection that fell behind says so rather than being logged as a transport fault.
            _hub.Disconnect(connection, connection.FellBehind ? "dropped: outbound queue full" : reason);
        }
    }

    private static async Task CloseQuietlyAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            await socket
                .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // The peer went away mid-handshake. There is nothing to answer and nothing to report.
        }
        catch (OperationCanceledException)
        {
            // The relay is shutting down; the socket closes with the process.
        }
    }

    private async Task PumpAsync(WebSocket socket, IRelayConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.ReceiveChunkBytes];

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Answer the close rather than dropping the socket. A client that closes
                    // gracefully waits for this frame, and without it a clean disconnect surfaces
                    // to the other side as an abnormal one — which the plugin would have to
                    // report as a lost connection (R-1.8) when nothing was lost.
                    await CloseQuietlyAsync(socket, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // A message larger than a session ever needs is the one way a client could make the
                // relay accumulate memory, which is state by another name. Refuse rather than grow.
                if (message.Length + result.Count > _options.MaxMessageBytes)
                {
                    _log.ConnectionRejected(connection.Id, "message exceeded MaxMessageBytes");
                    return;
                }

                message.Write(buffer.AsSpan(0, result.Count));
            }
            while (!result.EndOfMessage);

            await _hub.ReceiveAsync(connection, message.ToArray(), cancellationToken).ConfigureAwait(false);
        }
    }
}
