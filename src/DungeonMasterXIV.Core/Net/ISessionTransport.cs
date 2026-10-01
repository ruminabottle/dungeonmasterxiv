using System;

namespace DungeonMasterXIV.Net;

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
