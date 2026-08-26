using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Transport;

/// <summary>
/// The relay socket. The only place in the plugin that opens one, per the standards.
/// </summary>
/// <remarks>
/// <para>
/// D-2 permits exactly two network destinations: a configured session relay and a session peer.
/// This dials the address <see cref="SessionCoordinator"/> hands it, which
/// <see cref="RelayEndpoint"/> has already validated, and nothing else. There is no fallback host,
/// no discovery, and no address compiled in besides the default the user may replace.
/// </para>
/// <para>
/// Deliberately thin: it opens, closes and writes. Every decision about whether a connection should
/// exist belongs to <see cref="HostSession.RequiresRelayConnection"/>, so this cannot hold one open
/// through a rule it forgot.
/// </para>
/// </remarks>
public sealed class RelayTransport : ISessionTransport, IDisposable
{
    private readonly IPluginLog _log;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _lifetime;

    /// <param name="log">Dalamud's log. Never receives a character name (D-8).</param>
    public RelayTransport(IPluginLog log) => _log = log;

    /// <inheritdoc />
    public bool IsConnected => _socket?.State == WebSocketState.Open;

    /// <inheritdoc />
    public void Connect(Uri relay)
    {
        ArgumentNullException.ThrowIfNull(relay);
        Disconnect();

        _lifetime = new CancellationTokenSource();
        _socket = new ClientWebSocket();

        // Transport contract clause 2. WebSocket-level ping/pong, not an application heartbeat, so
        // no envelope and no C1 type is involved. The client initiates rather than relying on the
        // relay to: a lull long enough for a NAT table to drop the connection is normal play, and
        // the failure it prevents shows up mid-session rather than at connect time.
        _socket.Options.KeepAliveInterval = TransportContract.KeepAliveInterval;
        _socket.Options.KeepAliveTimeout = TransportContract.KeepAliveTimeout;

        // Logged without the address: a relay a user configured is their business, and the log is
        // the one artifact most likely to be pasted into a bug report.
        _log.Information("Connecting to the configured session relay.");

        _ = ConnectAsync(relay, _lifetime.Token);
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        if (_lifetime is not null)
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetime = null;
        }

        if (_socket is null)
        {
            return;
        }

        _socket.Dispose();
        _socket = null;
        _log.Information("Session relay connection closed.");
    }

    /// <inheritdoc />
    public void Send(byte[] envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (_socket is not { State: WebSocketState.Open } socket || _lifetime is null)
        {
            return;
        }

        _ = socket.SendAsync(envelope, WebSocketMessageType.Binary, endOfMessage: true, _lifetime.Token);
    }

    /// <inheritdoc />
    public void Dispose() => Disconnect();

    private async Task ConnectAsync(Uri relay, CancellationToken token)
    {
        try
        {
            if (_socket is not null)
            {
                await _socket.ConnectAsync(relay, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Disconnect() cancelled us. Expected, and not a failure worth logging.
        }
        catch (WebSocketException exception)
        {
            _log.Warning(exception, "Could not reach the session relay.");
        }
    }
}
