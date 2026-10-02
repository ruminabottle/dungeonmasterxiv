using System.Net.WebSockets;
using DungeonMasterXIV.Relay.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>Serves one WebSocket connection, passing messages to the hub; an oversized message ends it.</summary>
public sealed class WebSocketRelayEndpoint(
    RelayHub hub,
    ConnectionDirectory directory,
    RelayLog log,
    RelayOptions options,
    IHostApplicationLifetime lifetime)
{
    private readonly RelayHub _hub = hub;
    private readonly ConnectionDirectory _directory = directory;
    private readonly RelayLog _log = log;
    private readonly RelayOptions _options = options;

    private readonly IHostApplicationLifetime _lifetime = lifetime;

    public async Task ServeAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var connectionId = Guid.NewGuid().ToString("n");
        await using var connection = new WebSocketRelayConnection(connectionId, socket, _options.OutboundQueueCapacity);

        _directory.Add(connection);
        _log.ConnectionOpened(connectionId);

        var reason = "closed by peer";
        var closedCleanly = false;
        try
        {
            closedCleanly = await PumpAsync(socket, connection, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            reason = _lifetime.ApplicationStopping.IsCancellationRequested
                ? "relay shutting down"
                : "closed by peer without a close frame";
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
            await _hub
                .DisconnectAsync(
                    connection,
                    connection.FellBehind ? "dropped: outbound queue full" : reason,
                    cancellationToken,
                    closedCleanly && !connection.FellBehind)
                .ConfigureAwait(false);
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
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<bool> PumpAsync(WebSocket socket, IRelayConnection connection, CancellationToken cancellationToken)
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
                    await CloseQuietlyAsync(socket, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                if (message.Length + result.Count > _options.MaxMessageBytes)
                {
                    _log.ConnectionRejected(connection.Id, "message exceeded MaxMessageBytes");
                    return false;
                }

                message.Write(buffer.AsSpan(0, result.Count));
            }
            while (!result.EndOfMessage);

            await _hub.ReceiveAsync(connection, message.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
