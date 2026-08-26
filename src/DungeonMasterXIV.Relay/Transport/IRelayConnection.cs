namespace DungeonMasterXIV.Relay.Transport;

/// <summary>
/// One client attached to the relay, as the routing layer sees it: an id and a way to send bytes.
/// </summary>
/// <remarks>
/// The routing rules and the forensic log depend on this and not on a WebSocket, which is what lets
/// both be tested without a network and what keeps the framing decision confined to one adapter.
/// </remarks>
public interface IRelayConnection
{
    /// <summary>
    /// Identifies this connection for the lifetime of the connection and no longer. Generated fresh
    /// on connect, never derived from an address or anything a client sent, so it correlates log
    /// lines within one connection and nothing across two session codes (D-8).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Queues one complete envelope. Bytes go out exactly as given.
    /// </summary>
    /// <remarks>
    /// Returns once the bytes are queued, not once they are on the wire. A caller forwarding to
    /// several recipients must not be able to be held up by the slowest of them — see
    /// <see cref="WebSocketRelayConnection"/>.
    /// </remarks>
    ValueTask SendAsync(byte[] bytes, CancellationToken cancellationToken);

    /// <summary>
    /// Delivers anything already queued, then closes cleanly. Used after a refusal, so a rejected
    /// player receives the answer and is not left holding a live socket (R-1.3b).
    /// </summary>
    ValueTask CloseAsync(CancellationToken cancellationToken);

}
