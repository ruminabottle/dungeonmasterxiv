using System;

namespace DungeonMasterXIV.Net;

/// <summary>Passes the relay's transport notices on: a member dropped, the host's session reclaimed, the host away or back.</summary>
public readonly record struct TransportNotices(
    Action<byte[]>? OnConnectionDropped = null,
    Action? OnReclaimed = null,
    Action? OnReclaimRefused = null,
    Action? OnHostAway = null,
    Action? OnHostBack = null)
{
    public void Deliver(WireEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        switch (envelope.Type)
        {
            case WireMessageType.ConnectionDropped
                when OnConnectionDropped is { } onDropped && envelope.PublicKey is { } memberKey:
                onDropped(memberKey);
                break;
            case WireMessageType.Reclaimed:
                OnReclaimed?.Invoke();
                break;
            case WireMessageType.HostAway:
                OnHostAway?.Invoke();
                break;
            case WireMessageType.HostBack:
                OnHostBack?.Invoke();
                break;
        }
    }
}
