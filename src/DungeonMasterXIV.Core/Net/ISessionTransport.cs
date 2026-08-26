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
}
