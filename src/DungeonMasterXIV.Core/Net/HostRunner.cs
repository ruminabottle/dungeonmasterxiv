using System;

namespace DungeonMasterXIV.Net;

/// <summary>Starts and stops hosting: makes host keys, picks a session code, and releases resources on stop.</summary>
internal sealed class HostRunner
{
    private readonly HostSession _host;
    private readonly SessionResources _resources;
    private readonly OutboundHandshake _handshake;
    private readonly Func<SessionKeyExchange> _newKeys;
    private readonly Action _synchronise;

    public HostRunner(
        HostSession host,
        SessionResources resources,
        OutboundHandshake handshake,
        Func<SessionKeyExchange> newKeys,
        Action synchronise)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(handshake);
        ArgumentNullException.ThrowIfNull(newKeys);
        ArgumentNullException.ThrowIfNull(synchronise);

        _host = host;
        _resources = resources;
        _handshake = handshake;
        _newKeys = newKeys;
        _synchronise = synchronise;
    }

    public SessionKeyExchange? Keys { get; private set; }

    public void Start()
    {
        Keys?.Dispose();
        Keys = null;

        if (!SessionKeyPair.TryMake(_newKeys, out var hostKeys))
        {
            _host.Fail(SessionFailure.SessionKeysUnavailable);
            return;
        }

        Keys = hostKeys;
        _host.Start(SessionCodeGenerator.Next());
        _handshake.ForgetHostRegistration();
        _synchronise();
    }

    public void Stop()
    {
        _host.Stop();
        Keys?.Dispose();
        Keys = null;
        _resources.Release();
        _handshake.ForgetHostRegistration();
        _synchronise();
    }
}
