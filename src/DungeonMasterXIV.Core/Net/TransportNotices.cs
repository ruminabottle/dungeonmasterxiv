using System;

namespace DungeonMasterXIV.Net;

/// <summary>Passes the relay's connection-dropped notices on to a handler with the dropped member's key.</summary>
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
