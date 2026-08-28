using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The client's outbound handshake: the requests this client must SEND, and when they are due.
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="SessionCoordinator"/> because it is one job, and because that file was
/// over its limit.</b> Both requests here follow the same rule and got it wrong the same way twice:
/// BUG-36 for the host's code request, BUG-40 for the joiner's, each a factory with no production
/// call site. Keeping them in one type means the next message a client owes the relay is written
/// beside the two that were forgotten, rather than somewhere a third omission could hide.
/// </para>
/// <para>
/// <b>It sends; it decides nothing.</b> Phases, keys and failures belong to
/// <see cref="SessionCoordinator"/>, which is why the state arrives as collaborators rather than
/// being owned here. <see cref="RelayLink"/> owns the connection; this owns what goes down it.
/// </para>
/// </remarks>
internal sealed class OutboundHandshake
{
    private readonly RelayLink _link;
    private readonly HostSession _host;
    private readonly JoinAttempt _join;
    private readonly Func<SessionKeyExchange?> _joinerKeys;

    private string? _requestedCode;
    private string? _requestedJoinCode;
    private DisplayName _joinDisplayName;

    /// <summary>Wires the handshake to the state it reads and the link it sends down.</summary>
    /// <param name="link">The connection. Reports readiness; never decides.</param>
    /// <param name="host">The hosting half of the session.</param>
    /// <param name="join">The joining half of the session.</param>
    /// <param name="joinerKeys">The joiner's ephemeral keys, read at send time rather than captured.</param>
    public OutboundHandshake(
        RelayLink link,
        HostSession host,
        JoinAttempt join,
        Func<SessionKeyExchange?> joinerKeys)
    {
        _link = link;
        _host = host;
        _join = join;
        _joinerKeys = joinerKeys;
    }

    /// <summary>
    /// Whether the host's code request actually went out.
    /// </summary>
    /// <remarks>
    /// Set only after the socket reported ready and the request left, so it is the record of whether
    /// we ever got to speak. Without it a registration timeout cannot tell "the relay heard us and
    /// said nothing" from "we never reached the relay", and reported the first for both (BUG-38).
    /// </remarks>
    public bool RegistrationWasSent => _requestedCode is not null;

    /// <summary>What to call ourselves on the next join request (R-1.3e).</summary>
    /// <param name="name">The display name. Shown to the DM, never acted on.</param>
    public void JoiningAs(DisplayName name) => _joinDisplayName = name;

    /// <summary>Re-arms the host's code request, so a fresh code is claimed rather than assumed.</summary>
    public void ForgetHostRegistration() => _requestedCode = null;

    /// <summary>
    /// Re-arms the join request, so asking again for the SAME code re-sends.
    /// </summary>
    /// <remarks>
    /// R-1.3c makes that the ordinary case — a lapse means the DM was mid-encounter, not that they
    /// refused. The host's equivalent never needs it because R-1.2a regenerates on every refusal.
    /// </remarks>
    public void ForgetJoinRequest() => _requestedJoinCode = null;

    /// <summary>Sends whichever requests are due. Called once per frame.</summary>
    public void SendWhatIsDue()
    {
        RegisterWithRelayWhenReady();
        SendJoinRequestWhenReady();
    }

    /// <summary>
    /// Claims the session's code with the relay, once the socket can actually carry the request
    /// (R-1.2a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the step BUG-36 was missing entirely.</b> <c>WireEnvelope.ForCodeRequest</c> had no
    /// production call site at all: the host connected, sent nothing, and sat in
    /// <see cref="HostingPhase.Registering"/> until it timed out and told the DM the relay was
    /// unreachable — while the relay held the connection open waiting for the client to speak first.
    /// </para>
    /// <para>
    /// <b>On readiness, not on connection, and the difference is the whole reason this is here
    /// rather than in <see cref="SynchroniseTransport"/>.</b>
    /// <see cref="ISessionTransport.Send"/> discards a frame that arrives before the socket opens,
    /// and <see cref="ISessionTransport.IsConnected"/> is already true while a connect is in flight.
    /// Sending on the return from <c>Connect</c> would therefore have produced the same silence
    /// through a different door — and left a fix that looked right in review and failed in the
    /// product.
    /// </para>
    /// <para>
    /// Guarded by <b>which code was requested</b> rather than by a "have we sent one" flag. R-1.2a
    /// answers a refusal by regenerating and asking again, so the interesting question is whether
    /// the code currently held has been claimed — a boolean would be true after the refused attempt
    /// and the replacement code would never be requested.
    /// </para>
    /// </remarks>
    private void RegisterWithRelayWhenReady()
    {
        if (_host.Phase != HostingPhase.Registering
            || _host.Code is not { } code
            || string.Equals(_requestedCode, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _requestedCode = code.Value;
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForCodeRequest(code)));
    }

    /// <summary>
    /// Asks to be admitted, once the socket can actually carry the request (R-1.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>BUG-40, and it is BUG-36's twin one message along.</b> <c>WireEnvelope.ForJoinRequest</c>
    /// had no production call site at all: the joiner connected, sent nothing, and sat in
    /// <see cref="JoinPhase.Contacting"/> until it timed out and told the player the relay was
    /// unreachable — while the relay held the connection open waiting for the client to speak. The
    /// host half of this was found and fixed and nobody asked the same question of this side.
    /// </para>
    /// <para>
    /// <b>On readiness, not on connection</b>, for the reason
    /// <see cref="RegisterWithRelayWhenReady"/> records: <see cref="ISessionTransport.Send"/>
    /// discards a frame that arrives before the socket opens, and <c>IsConnected</c> is already true
    /// while a connect is in flight. Sending from <c>SessionCoordinator.RequestJoin</c> would look right and
    /// reproduce BUG-40 with a fix in place.
    /// </para>
    /// <para>
    /// <b>This sends <see cref="WireEnvelope.ForJoinRequest(SessionCode, byte[])"/> and never
    /// <see cref="WireEnvelope.ForRelinkRequest"/>.</b> That is a decision, not an oversight: no
    /// production path reaches a relink. Nothing on this side holds the participant id a claim would
    /// carry, and nothing on the host side reads <c>ClaimedParticipantId</c> back off the wire, so
    /// wiring the relink factory here would need both ends invented. Making a relink send a plain
    /// join request to look complete is the specific thing that must not happen — R-1.5's claim would
    /// be silently dropped while every test passed.
    /// </para>
    /// </remarks>
    private void SendJoinRequestWhenReady()
    {
        if (_join.Phase != JoinPhase.Contacting
            || _join.Code is not { } code
            || _joinerKeys() is not { } keys
            || string.Equals(_requestedJoinCode, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _requestedJoinCode = code.Value;
        _link.Send(EnvelopeCodec.Encode(
            WireEnvelope.ForJoinRequest(code, keys.PublicKey, _joinDisplayName)));
    }
}
