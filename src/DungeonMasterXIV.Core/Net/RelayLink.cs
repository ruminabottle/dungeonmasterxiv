using System;

namespace DungeonMasterXIV.Net;

/// <summary>Connects to or disconnects from the relay on demand and passes on frames and reported failures.</summary>
public sealed class RelayLink
{
    private readonly ISessionTransport _transport;
    private readonly Func<string> _relayAddress;
    private readonly Action<byte[]> _onFrame;
    private readonly object _reportedFailureLock = new();
    private SessionFailure _reportedFailure = SessionFailure.None;

    public RelayLink(ISessionTransport transport, Func<string> relayAddress, Action<byte[]> onFrame)
    {
        _transport = transport;
        _relayAddress = relayAddress;
        _onFrame = onFrame;
        _transport.Failed += OnTransportFailed;
        _transport.Received += _onFrame;
    }

    public bool IsReadyToSend => _transport.IsReadyToSend;

    public void Send(byte[] envelope) => _transport.Send(envelope);

    public SessionFailure Synchronise(bool wanted)
    {
        if (wanted && !_transport.IsConnected)
        {
            if (!RelayEndpoint.TryParse(_relayAddress(), out var relay))
            {
                return SessionFailure.RelayAddressUnreadable;
            }

            _transport.Connect(relay!);
            return SessionFailure.None;
        }

        if (!wanted && _transport.IsConnected)
        {
            _transport.Disconnect();
        }

        return SessionFailure.None;
    }

    public bool TryTakeReportedFailure(out SessionFailure failure)
    {
        lock (_reportedFailureLock)
        {
            failure = _reportedFailure;
            _reportedFailure = SessionFailure.None;
        }

        return failure != SessionFailure.None;
    }

    public void Detach()
    {
        _transport.Failed -= OnTransportFailed;
        _transport.Received -= _onFrame;
    }

    private void OnTransportFailed(SessionFailure failure)
    {
        lock (_reportedFailureLock)
        {
            _reportedFailure = failure;
        }
    }
}
