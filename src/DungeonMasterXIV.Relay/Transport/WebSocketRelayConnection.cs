using System.Net.WebSockets;
using System.Threading.Channels;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>
/// An <see cref="IRelayConnection"/> over one WebSocket, with its own outbound queue.
/// </summary>
/// <remarks>
/// <para>
/// One envelope per binary WebSocket message, which is the framing half of the contract with the
/// plugin's connection adapter.
/// </para>
/// <para>
/// <b>Sending is queued rather than awaited, and that is the point of this type.</b> Forwarding used
/// to write to each recipient inline on the sending client's receive pump, so one participant who
/// stopped reading — a suspended laptop, a wedged client — stalled every delivery in that session.
/// The blast radius was a whole session, and the symptom reads to a player as a network problem
/// rather than as us, which is the kind of failure that gets debugged for an hour in the wrong place.
/// Here a forward hands bytes to a queue and returns; the socket is written by this connection's own
/// pump, so a slow reader can only ever slow itself.
/// </para>
/// <para>
/// The queue is <b>bounded</b>, because an unbounded one is just the stall converted into memory that
/// accumulates — which D-2 forbids for its own reasons. A client that falls further behind than the
/// bound is not keeping up, so it is dropped rather than buffered indefinitely: the session survives
/// the participant, which is the trade this whole design is making.
/// </para>
/// </remarks>
public sealed class WebSocketRelayConnection : IRelayConnection, IAsyncDisposable
{
    private readonly WebSocket _socket;
    private readonly Channel<byte[]> _outbound;
    private readonly CancellationTokenSource _aborting = new();
    private readonly Task _pump;

    /// <summary>Wraps <paramref name="socket"/>, starting its outbound pump.</summary>
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

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Whether this connection was dropped for falling behind rather than for anything it did. Read
    /// by the endpoint so the forensic log can say which it was (A-1.5a-r).
    /// </summary>
    public bool FellBehind { get; private set; }

    /// <inheritdoc />
    public ValueTask SendAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (_outbound.Writer.TryWrite(bytes))
        {
            return ValueTask.CompletedTask;
        }

        // Full: this client has stopped draining. Drop it here rather than let it hold up the
        // sender, and abort rather than close politely — a peer that is not reading will not
        // complete a close handshake either.
        FellBehind = true;
        _outbound.Writer.TryComplete();
        _socket.Abort();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        // Stop accepting, then let the pump finish what is already queued BEFORE closing. A refusal
        // is delivered and then the connection closes (R-1.3b); closing first would deliver silence,
        // which is the failure R-1.3b exists to remove.
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
            // The peer is already gone. The connection is closed either way.
        }
    }

    /// <inheritdoc />
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
            // Shutting down.
        }
        catch (WebSocketException)
        {
            // The peer went away mid-send. Its receive pump reports the disconnect.
        }
    }
}
