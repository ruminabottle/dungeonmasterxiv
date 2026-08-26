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
    bool IsConnected { get; }

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
}
