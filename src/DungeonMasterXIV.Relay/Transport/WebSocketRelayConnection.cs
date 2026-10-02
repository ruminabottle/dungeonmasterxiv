using System.Net.WebSockets;
using System.Threading.Channels;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>A relay connection over a WebSocket that queues outbound messages and aborts if the queue fills.</summary>
public sealed class WebSocketRelayConnection : IRelayConnection, IAsyncDisposable
{
    private readonly WebSocket _socket;
    private readonly Channel<byte[]> _outbound;
    private readonly CancellationTokenSource _aborting = new();
    private readonly Task _pump;

    public WebSocketRelayConnection(string id, WebSocket socket, int queueCapacity)
    {
        Id = id;
        _socket = socket;
        _outbound = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(queueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        _pump = Task.Run(DrainAsync);
    }

    public string Id { get; }

    public bool FellBehind { get; private set; }

    public ValueTask SendAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (_outbound.Writer.TryWrite(bytes))
        {
            return ValueTask.CompletedTask;
        }

        FellBehind = true;
        _outbound.Writer.TryComplete();
        _socket.Abort();
        return ValueTask.CompletedTask;
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        _outbound.Writer.TryComplete();
        await _pump.ConfigureAwait(false);

        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _outbound.Writer.TryComplete();
        await _aborting.CancelAsync().ConfigureAwait(false);
        await _pump.ConfigureAwait(false);
        _aborting.Dispose();
    }

    private async Task DrainAsync()
    {
        try
        {
            await foreach (var bytes in _outbound.Reader.ReadAllAsync(_aborting.Token).ConfigureAwait(false))
            {
                if (_socket.State != WebSocketState.Open)
                {
                    return;
                }

                await _socket
                    .SendAsync(bytes, WebSocketMessageType.Binary, endOfMessage: true, _aborting.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }
}
