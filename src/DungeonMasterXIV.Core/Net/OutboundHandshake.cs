using System;

namespace DungeonMasterXIV.Net;

internal sealed class OutboundHandshake
{
    private readonly RelayLink _link;
    private readonly HostSession _host;
    private readonly JoinAttempt _join;
    private readonly Func<SessionKeyExchange?> _joinerKeys;

    private string? _requestedCode;
    private string? _requestedJoinCode;
    private DisplayName _joinDisplayName;
    private Guid? _claimedParticipantId;
    private string? _reportedCanCompareFor;

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

    public bool RegistrationWasSent => _requestedCode is not null;

    public void JoiningAs(DisplayName name, Guid? claimedParticipantId)
    {
        _joinDisplayName = name;
        _claimedParticipantId = claimedParticipantId;
    }

    public void ForgetHostRegistration() => _requestedCode = null;

    public void ForgetJoinRequest()
    {
        _requestedJoinCode = null;
        _reportedCanCompareFor = null;
    }

    public void SendWhatIsDue()
    {
        RegisterWithRelayWhenReady();
        SendJoinRequestWhenReady();
        ReportWeCanCompareWhenWeCan();
    }

    private void ReportWeCanCompareWhenWeCan()
    {
        if (_join.Phase is not (JoinPhase.Contacting or JoinPhase.AwaitingDecision)
            || _join.Fingerprint is null
            || _join.Code is not { } code
            || _joinerKeys() is not { } keys
            || string.Equals(_reportedCanCompareFor, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _reportedCanCompareFor = code.Value;
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForJoinerHoldsFingerprint(code, keys.PublicKey)));
    }

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
        _link.Send(EnvelopeCodec.Encode(_claimedParticipantId is { } claimed
            ? WireEnvelope.ForRelinkRequest(code, keys.PublicKey, claimed)
            : WireEnvelope.ForJoinRequest(code, keys.PublicKey, _joinDisplayName)));
    }
}
