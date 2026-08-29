using System;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The relay socket. The only place in this product that opens one, per the standards.
/// </summary>
/// <remarks>
/// <para>
/// D-2 permits exactly two network destinations: a configured session relay and a session peer.
/// This dials the address <see cref="SessionCoordinator"/> hands it, which
/// <see cref="RelayEndpoint"/> has already validated, and nothing else. There is no fallback host,
/// no discovery, and no address compiled in besides the default the user may replace.
/// </para>
/// <para>
/// It lives in Core rather than beside the plugin's other Dalamud-touching code for one reason: a
/// socket nothing can construct is a socket nothing can test. Its only Dalamud dependency was the
/// log, so the log became a seam and the mechanics came with it.
/// </para>
/// <para>
/// Deliberately thin: it opens, closes and writes. Every decision about whether a connection should
/// exist belongs to <see cref="HostSession.RequiresRelayConnection"/>, so this cannot hold one open
/// through a rule it forgot.
/// </para>
/// </remarks>
public sealed class WebSocketSessionTransport : ISessionTransport, IDisposable
{
    private readonly ISessionTransportLog _log;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _lifetime;
    private bool _connecting;

    // BUG-122. The socket whose ConnectAsync has actually RETURNED. Held as the reference rather
    // than a bool so a reconnect cannot inherit the previous socket's readiness: the check is
    // "the sendable one IS the current one", which a stale flag could not express.
    private volatile ClientWebSocket? _connected;

    /// <param name="log">
    /// Where this type reports. An abstraction rather than Dalamud's <c>IPluginLog</c>, so the
    /// transport lives in a project a test can reference — which is what makes the socket
    /// reachable from a test at all. Never receives a character name or a relay address (D-8).
    /// </param>
    public WebSocketSessionTransport(ISessionTransportLog log) => _log = log;

    /// <inheritdoc />
    public event Action<SessionFailure>? Failed;

    /// <inheritdoc />
    public event Action<byte[]>? Received;

    /// <summary>
    /// Whether a connection exists or is being established.
    /// </summary>
    /// <remarks>
    /// A connect in flight counts. Reporting false while <see cref="WebSocketState.Connecting"/>
    /// would let <see cref="SessionCoordinator.SynchroniseTransport"/> start a second connect on top
    /// of the first — reachable by clicking Start session and then Request to join before the first
    /// one lands.
    /// </remarks>
    public bool IsConnected => _connecting || _socket?.State == WebSocketState.Open;

    /// <summary>
    /// Whether a frame sent right now would actually go out.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="IsConnected"/>, which deliberately counts a connect in flight so
    /// that <see cref="SessionCoordinator.SynchroniseTransport"/> does not stack a second one. That
    /// conflation leaves callers no way to ask the different question "is it safe to send yet",
    /// and <see cref="Send"/> drops a frame that arrives before the socket opens.
    /// <para>
    /// <b>BUG-122: THIS ASKED THE SOCKET AND THE SOCKET ANSWERED TOO EARLY.</b> It used to be
    /// <c>_socket?.State == WebSocketState.Open</c>, and <see cref="ClientWebSocket.State"/> reports
    /// <see cref="WebSocketState.Open"/> BEFORE <c>ClientWebSocket.ConnectAsync</c> has
    /// returned — at which point <c>ClientWebSocket.SendAsync</c> still throws
    /// <see cref="InvalidOperationException"/> <i>"The WebSocket is not connected"</i>. Measured: of
    /// 300 sends issued the instant <c>State</c> read <c>Open</c>, <b>191 threw</b>. So the property
    /// documented above as "would actually go out" was answering a different question, and every
    /// caller that trusted it — including <see cref="Send"/>'s own guard — inherited the error.
    /// </para>
    /// <para>
    /// It now reports on the connect having COMPLETED, which is the thing callers were asking about.
    /// The <c>State</c> check is kept as well: a connect that finished is not a socket that is still
    /// open.
    /// </para>
    /// <para>
    /// The remark this replaces said the shape was "not reachable in the product today". It was:
    /// a full-suite run reached it through <c>SessionCoordinator.ReceiveJoinRequest</c> roughly once
    /// in fifty, and the exception left <see cref="Send"/> — which is documented to DROP — and
    /// travelled into the caller.
    /// </para>
    /// </remarks>
    public bool IsReadyToSend =>
        ReferenceEquals(_connected, _socket) && _socket?.State == WebSocketState.Open;

    /// <inheritdoc />
    public void Connect(Uri relay)
    {
        ArgumentNullException.ThrowIfNull(relay);
        Disconnect();

        var lifetime = new CancellationTokenSource();
        var socket = new ClientWebSocket();
        _lifetime = lifetime;
        _socket = socket;
        _connecting = true;

        // Transport contract clause 2. WebSocket-level ping/pong, not an application heartbeat, so
        // no envelope and no C1 type is involved. The client initiates rather than relying on the
        // relay to: a lull long enough for a NAT table to drop the connection is normal play, and
        // the failure it prevents shows up mid-session rather than at connect time.
        _socket.Options.KeepAliveInterval = TransportContract.KeepAliveInterval;
        _socket.Options.KeepAliveTimeout = TransportContract.KeepAliveTimeout;

        // Kept so a refused upgrade can be read (R-1.7b). Without it the status and headers are
        // discarded and a version mismatch is indistinguishable from an unreachable relay, which is
        // the generic failure R-1.7b exists to prevent.
        _socket.Options.CollectHttpResponseDetails = true;

        // Logged without the address: a relay a user configured is their business, and the log is
        // the one artifact most likely to be pasted into a bug report.
        _log.Information("Connecting to the configured session relay.");

        // The socket and token are passed as locals, never re-read from the fields. A body that
        // read _socket could observe a LATER connection's socket after a Disconnect/Connect pair and
        // drive somebody else's object.
        // The version travels on the connect request itself, so a mismatch is refused before a
        // socket exists rather than after one is established (R-1.7b).
        _ = ConnectAsync(socket, ProtocolVersion.AppendTo(relay), lifetime.Token);
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        _connecting = false;
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

        // Cleared before the close is attempted, not after: the close is a bounded wait, and for its
        // duration IsConnected must already read false rather than reporting a connection that is on
        // its way out.
        var socket = _socket;
        _socket = null;
        _connected = null;

        CloseThenDispose(socket);
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

    // BUG-5. Disposing a socket never puts a close frame on the wire, so the relay is not told and
    // holds the connection until its own idle reaper fires -- which then absorbs every ordinary
    // disconnect as though it were a client that vanished. The output-only close is deliberate: this
    // end has nothing further to say and does not need the peer's reply, so there is no round trip
    // to wait on. A socket that never reached Open has no frame to send and is only disposed.
    private void CloseThenDispose(ClientWebSocket socket)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            socket.Dispose();
            return;
        }

        var failure = TransportShutdown.CloseThenDispose(
            token => socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, token),
            socket.Dispose,
            TransportShutdown.CloseTimeout);

        if (failure is not null)
        {
            _log.Warning(failure, "The session relay connection was disposed without a completed close handshake.");
        }
    }

    /// <summary>
    /// Reads the refusal off the socket and hands the decision to the contract.
    /// </summary>
    /// <remarks>
    /// Thin on purpose: everything but reading two values off a Dalamud-adjacent object lives in
    /// <see cref="ProtocolVersion.ClassifyRefusal"/>, where it can be tested without a socket.
    /// </remarks>
    private static SessionFailure ClassifyRefusal(ClientWebSocket socket)
    {
        var stated = socket.HttpResponseHeaders is not null
            && socket.HttpResponseHeaders.TryGetValue(ProtocolVersion.Header, out var values)
                ? values.FirstOrDefault()
                : null;

        return ProtocolVersion.ClassifyRefusal(
            socket.HttpStatusCode == HttpStatusCode.UpgradeRequired,
            stated);
    }

    private async Task ConnectAsync(ClientWebSocket socket, Uri relay, CancellationToken token)
    {
        try
        {
            await socket.ConnectAsync(relay, token).ConfigureAwait(false);

            // BUG-122: THE ONLY MOMENT AT WHICH SENDING IS ACTUALLY SAFE. Recorded here rather than
            // inferred from socket state, because the state says Open well before this line runs.
            _connected = socket;

            await ReceiveLoopAsync(socket, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Disconnect() cancelled us. Expected, and not a failure worth reporting.
        }
        catch (Exception exception) when (exception is WebSocketException
                                              or ObjectDisposedException
                                              or InvalidOperationException)
        {
            // Narrower catches let ObjectDisposedException and InvalidOperationException escape into
            // an unobserved task, where the failure vanishes and the user waits on a spinner.
            var failure = ClassifyRefusal(socket);
            _log.Warning(
                exception,
                failure == SessionFailure.RelayUnreachable
                    ? "Could not reach the session relay."
                    : "The session relay refused this build's protocol version.");

            Failed?.Invoke(failure);
        }
        finally
        {
            if (ReferenceEquals(socket, _socket))
            {
                _connecting = false;
            }
        }
    }

    /// <summary>
    /// Reads frames until the socket closes or we are cancelled.
    /// </summary>
    /// <remarks>
    /// One <see cref="WireEnvelope"/> per frame, per the transport contract, so a complete message
    /// is a complete frame and no reassembly is needed at this layer. A frame larger than the buffer
    /// is read across continuations rather than truncated — truncating would hand the decoder a
    /// prefix that might still parse, which is worse than a frame that plainly does not.
    /// </remarks>
    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[8192];

        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            using var frame = new MemoryStream();
            ValueWebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(), token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Failed?.Invoke(SessionFailure.ConnectionLost);
                    return;
                }

                frame.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            Received?.Invoke(frame.ToArray());
        }
    }
}
