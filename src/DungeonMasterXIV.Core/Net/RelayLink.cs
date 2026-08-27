using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Whether a relay connection should exist, and how a failure raised off the socket thread reaches
/// the session that cares about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="SessionCoordinator"/> on the standard's own test — the number of
/// reasons the file could change.</b> Two changes were made to that class in one evening, C22's
/// pending-notice work and BUG-36's registration handshake, and <b>neither touched a line that is
/// now in this file.</b> Both were about what a session does; nothing here is. That is the evidence
/// the seam is real rather than a trim to fit a line count.
/// </para>
/// <para>
/// <b>The lock is the sharpest tell.</b> Marshalling a callback from the socket thread onto the
/// frame the plugin ticks is a concurrency concern, and it sat in the middle of a class whose other
/// members are session rules with no thread-safety dimension at all. A reader auditing the locking
/// had to read past hosting, joining and admission to find both halves of it.
/// </para>
/// <para>
/// <b>What this does NOT claim.</b> It owns the connection's <i>lifecycle</i> and its <i>failure
/// marshalling</i> — not exclusive ownership of the socket object.
/// <see cref="AdmissionAnnouncer"/> still sends through the same transport, because sending is a
/// session act and routing every send through here would make this type a passthrough wrapper,
/// which is the shape that adds a file and no boundary. Stated rather than left for a reviewer to
/// notice.
/// </para>
/// </remarks>
public sealed class RelayLink
{
    private readonly ISessionTransport _transport;
    private readonly Func<string> _relayAddress;
    private readonly Action<byte[]> _onFrame;
    private readonly object _reportedFailureLock = new();
    private SessionFailure _reportedFailure = SessionFailure.None;

    /// <param name="transport">The socket adapter. Owned by the caller; never opened at construction.</param>
    /// <param name="relayAddress">
    /// Reads the configured relay at the moment of connecting rather than at construction, so
    /// changing it in settings takes effect on the next session without a reload (R-1.8).
    /// </param>
    /// <param name="onFrame">Where arriving frames go. Subscribed here so teardown has one place to undo.</param>
    public RelayLink(ISessionTransport transport, Func<string> relayAddress, Action<byte[]> onFrame)
    {
        _transport = transport;
        _relayAddress = relayAddress;
        _onFrame = onFrame;
        _transport.Failed += OnTransportFailed;
        _transport.Received += _onFrame;
    }

    /// <summary>Whether a frame sent right now would actually go out.</summary>
    /// <remarks>
    /// Distinct from being connected, and the distinction is load-bearing: the socket reports itself
    /// connected while a connect is still in flight, and a frame sent then is discarded silently
    /// (BUG-36).
    /// </remarks>
    public bool IsReadyToSend => _transport.IsReadyToSend;

    /// <summary>Sends one already-encoded envelope.</summary>
    public void Send(byte[] envelope) => _transport.Send(envelope);

    /// <summary>
    /// Brings the socket into line with whether a session needs one.
    /// </summary>
    /// <remarks>
    /// R-1.1's invariant lives here and in <see cref="HostSession.RequiresRelayConnection"/> and
    /// nowhere else, so there is one answer to "should we be connected" rather than a rule each call
    /// site is trusted to remember. The caller decides <i>whether</i> a connection is wanted; this
    /// decides what to do about it.
    /// </remarks>
    /// <param name="wanted">Whether any part of the session needs a connection right now.</param>
    /// <returns>
    /// <see cref="SessionFailure.None"/>, or the failure the caller should apply. Returned rather
    /// than applied: failing is a session act — it ends hosting, starts a grace window, and calls
    /// back into this method — and a type that owns a socket must not also decide what a session
    /// does about one.
    /// </returns>
    public SessionFailure Synchronise(bool wanted)
    {
        if (wanted && !_transport.IsConnected)
        {
            if (!RelayEndpoint.TryParse(_relayAddress(), out var relay))
            {
                // NOT RelayUnreachable. The address never parsed, so no socket was opened and
                // nothing was contacted — this build has learned nothing about the relay, and
                // saying it is unreachable blames a third party for the operator's own typo.
                // See BUG-37.
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

    /// <summary>
    /// Takes the failure the transport reported since the last call, if there was one.
    /// </summary>
    /// <remarks>
    /// Recorded on the socket thread and applied on the caller's tick. Mutating session state from a
    /// receive callback races the draw, so the two are deliberately separated — a failure that
    /// changes nothing until the next tick is an assertion rather than a hope.
    /// </remarks>
    public bool TryTakeReportedFailure(out SessionFailure failure)
    {
        lock (_reportedFailureLock)
        {
            failure = _reportedFailure;
            _reportedFailure = SessionFailure.None;
        }

        return failure != SessionFailure.None;
    }

    /// <summary>Unsubscribes from the transport. Wired into the plugin's teardown.</summary>
    public void Detach()
    {
        _transport.Failed -= OnTransportFailed;
        _transport.Received -= _onFrame;
    }

    // Raised off the framework thread by the transport, so it is only recorded here and applied on
    // the next tick.
    private void OnTransportFailed(SessionFailure failure)
    {
        lock (_reportedFailureLock)
        {
            _reportedFailure = failure;
        }
    }
}
