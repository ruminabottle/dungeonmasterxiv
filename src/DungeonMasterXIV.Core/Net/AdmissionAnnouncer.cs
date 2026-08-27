using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What a joiner is told, and when. The third responsibility that grew out of
/// <see cref="SessionCoordinator"/> once admission decisions started reaching the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>This takes a transport; it never opens one.</b> Sockets live in the plugin's <c>Net/</c> and
/// nowhere else, so "answering the door" must not become a second place that dials.
/// </para>
/// <para>
/// Separate from <see cref="AdmissionDesk"/> on purpose. The desk knows who is waiting and knows
/// nothing about sockets, which is what lets it be unit tested without one — a seam that keeps the
/// pure type pure is worth more here than tidiness, because anything that acquires a transport
/// dependency stops being testable on this machine.
/// </para>
/// </remarks>
public sealed class AdmissionAnnouncer
{
    private readonly ISessionTransport _transport;

    /// <param name="transport">Where answers go. Owned by the caller, never opened here.</param>
    public AdmissionAnnouncer(ISessionTransport transport) => _transport = transport;

    /// <summary>
    /// Tells a joiner the DM is looking at their request, carrying the host's key so they can
    /// compare the fingerprint <b>while the decision is still open</b> (R-1.3a-i, A-1.3f-1).
    /// </summary>
    /// <remarks>
    /// Sent when the request is recorded, not when it is answered. That timing is the requirement:
    /// the same key travels again in <see cref="Accepted"/>, and a build that sends it only there
    /// gives the joiner nothing to compare until the comparison is moot.
    /// </remarks>
    public void Pending(
        SessionCode code,
        byte[] joinerPublicKey,
        byte[] hostPublicKey,
        AdmissionDeadline deadline) =>
        Send(WireEnvelope.ForJoinPending(code, joinerPublicKey, hostPublicKey, deadline));

    /// <summary>
    /// Tells a joiner they are in, carrying <b>both</b> keys.
    /// </summary>
    /// <remarks>
    /// The joiner's is echoed so they can tell which of several outstanding requests was answered;
    /// the host's is the half they cannot obtain any other way. Omitting the host's key admits
    /// somebody who is routed and permanently unable to decrypt, which reads as an encryption bug.
    /// </remarks>
    public void Accepted(SessionCode code, byte[] joinerPublicKey, byte[] hostPublicKey) =>
        Send(WireEnvelope.ForJoinAccepted(code, joinerPublicKey, hostPublicKey));

    /// <summary>
    /// Tells a joiner they were refused (R-1.3b).
    /// </summary>
    /// <remarks>
    /// Denial is an explicit message — not a timeout, not silence, not an absence of acceptance. A
    /// refused player who gets silence cannot distinguish refusal from a broken relay, a wrong code,
    /// or a DM who has not looked yet, which is R-1.8's ambiguity arriving through another door.
    /// </remarks>
    public void Denied(SessionCode code, byte[] joinerPublicKey) =>
        Send(WireEnvelope.ForJoinDenied(code, joinerPublicKey));

    /// <summary>
    /// Tells requesters whose window closed that it <b>lapsed</b> — never that they were denied.
    /// </summary>
    /// <remarks>
    /// Nobody refused them; the DM may have been mid-encounter. So asking again is reasonable and
    /// R-1.3c requires them to be told that, rather than being left in the fifteen silent minutes
    /// the requirement exists to end.
    /// </remarks>
    public void Lapsed(SessionCode code, IEnumerable<PendingAdmission> lapsed)
    {
        ArgumentNullException.ThrowIfNull(lapsed);

        foreach (var request in lapsed)
        {
            if (request.JoinerPublicKey is { } joinerKey)
            {
                Send(WireEnvelope.ForJoinLapsed(code, joinerKey));
            }
        }
    }

    private void Send(WireEnvelope envelope) => _transport.Send(EnvelopeCodec.Encode(envelope));
}
