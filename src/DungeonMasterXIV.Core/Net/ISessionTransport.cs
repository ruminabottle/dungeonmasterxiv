using System;

namespace DungeonMasterXIV.Net;

/// <summary>A connection to the relay that sends and receives envelope bytes and reports failures.</summary>
public interface ISessionTransport
{
    bool IsConnected { get; }

    bool IsReadyToSend { get; }

    void Connect(Uri relay);

    void Disconnect();

    void Send(byte[] envelope);

    event Action<SessionFailure>? Failed;

    event Action<byte[]>? Received;
}
