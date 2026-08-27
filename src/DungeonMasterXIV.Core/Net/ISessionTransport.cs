using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The socket, as the session layer sees it. Implemented in the plugin's <c>Net/</c>, where the
/// standards require sockets to live; declared here so the coordinator that drives it is testable
/// without one.
/// </summary>
public interface ISessionTransport
{
    /// <summary>Whether a relay connection is currently held.</summary>
    /// <remarks>
    /// True while a connect is still in flight, deliberately, so
    /// <see cref="SessionCoordinator.SynchroniseTransport"/> does not stack a second one. It is
    /// therefore <b>not</b> the right question to ask before sending — see
    /// <see cref="IsReadyToSend"/>.
    /// </remarks>
    bool IsConnected { get; }

    /// <summary>Whether a frame sent right now would actually go out.</summary>
    /// <remarks>
    /// <para>
    /// <b>On the interface because BUG-36 made it load-bearing.</b> It existed on the WebSocket
    /// implementation, named as a hazard that was "not reachable in the product today". Registering
    /// a session made it reachable: the host must send its <c>CodeRequest</c> once connected, and
    /// <see cref="Send"/> silently discards a frame that arrives before the socket opens. Sending on
    /// the return from <see cref="Connect"/> would have reproduced BUG-36 exactly — a host that
    /// believes it registered, a relay that was never told, and no error anywhere.
    /// </para>
    /// <para>
    /// So the coordinator sends on readiness rather than on connection, which it cannot do unless it
    /// can ask.
    /// </para>
    /// </remarks>
    bool IsReadyToSend { get; }

    /// <summary>Opens a connection to <paramref name="relay"/>.</summary>
    void Connect(Uri relay);

    /// <summary>Closes the connection. Safe to call when nothing is open.</summary>
    void Disconnect();

    /// <summary>Sends one already-encoded envelope.</summary>
    void Send(byte[] envelope);

    /// <summary>
    /// Raised when the transport itself fails. Without this the session layer cannot distinguish a
    /// relay that refused from one that is merely slow, and a refusal would only ever surface as a
    /// timeout — which is why <see cref="SessionFailure.ConnectionLost"/> would otherwise be
    /// unreachable in the product however well the type describes it.
    /// </summary>
    /// <remarks>
    /// May be raised off the framework thread. Subscribers must not touch session state directly;
    /// <see cref="SessionCoordinator"/> queues it and applies it on the next tick.
    /// </remarks>
    event Action<SessionFailure>? Failed;

    /// <summary>
    /// Raised with each frame that arrives. One encoded <see cref="WireEnvelope"/> per frame, per
    /// the transport contract.
    /// </summary>
    /// <remarks>
    /// Bytes rather than a decoded envelope on purpose: anything can arrive from a relay, so
    /// deciding whether it parsed belongs to the layer that also decides whether to trust it. May be
    /// raised off the framework thread — <see cref="SessionCoordinator"/> queues and applies on the
    /// next tick rather than mutating session state from a socket callback.
    /// </remarks>
    event Action<byte[]>? Received;
}
