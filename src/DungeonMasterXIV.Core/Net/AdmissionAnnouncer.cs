using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>Sends the host's pending, accepted, denied and lapsed answers to joiners over the transport.</summary>
public sealed class AdmissionAnnouncer
{
    private readonly ISessionTransport _transport;

    public AdmissionAnnouncer(ISessionTransport transport) => _transport = transport;

    public void Pending(
        SessionCode code,
        byte[] joinerPublicKey,
        byte[] hostPublicKey,
        AdmissionDeadline deadline) =>
        Send(WireEnvelope.ForJoinPending(code, joinerPublicKey, hostPublicKey, deadline));

    public void HostKey(SessionCode code, byte[] joinerPublicKey, byte[] hostPublicKey) =>
        Send(WireEnvelope.ForHostKey(code, joinerPublicKey, hostPublicKey));

    public void Accepted(
        SessionCode code,
        byte[] joinerPublicKey,
        byte[] hostPublicKey,
        SealedPayload? welcome = null) =>
        Send(WireEnvelope.ForJoinAccepted(code, joinerPublicKey, hostPublicKey, welcome));

    public void Denied(SessionCode code, byte[] joinerPublicKey) =>
        Send(WireEnvelope.ForJoinDenied(code, joinerPublicKey));

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
