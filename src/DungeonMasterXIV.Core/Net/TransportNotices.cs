using System;

namespace DungeonMasterXIV.Net;

public readonly record struct TransportNotices(
    Action<byte[]>? OnConnectionDropped = null)
{
    public void Deliver(WireEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (OnConnectionDropped is { } onDropped && envelope.PublicKey is { } memberKey)
        {
            onDropped(memberKey);
        }
    }
}
