using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Sends the relay registration, the join hello and the sealed join request once each is due and the link is ready.</summary>
internal sealed class OutboundHandshake
{
    private readonly RelayLink _link;
    private readonly HostSession _host;
    private readonly JoinAttempt _join;
    private readonly Func<SessionKeyExchange?> _joinerKeys;
    private readonly Func<byte[]?> _reclaimSecret;
    private readonly Func<bool> _hostAway;

    private string? _requestedCode;
    private string? _helloSentFor;
    private string? _requestedJoinCode;
    private DisplayName _joinDisplayName;
    private Guid? _claimedParticipantId;
    private bool _reclaimSentOnThisLink;

    public OutboundHandshake(
        RelayLink link,
        HostSession host,
        JoinAttempt join,
        Func<SessionKeyExchange?> joinerKeys,
        Func<byte[]?> reclaimSecret,
        Func<bool> hostAway)
    {
        _link = link;
        _host = host;
        _join = join;
        _joinerKeys = joinerKeys;
        _reclaimSecret = reclaimSecret;
        _hostAway = hostAway;
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
        _helloSentFor = null;
        _requestedJoinCode = null;
    }

    public void SendWhatIsDue()
    {
        if (!_link.IsReadyToSend)
        {
            _reclaimSentOnThisLink = false;
        }

        ReclaimWhenReconnected();

        RegisterWithRelayWhenReady();
        SendHelloWhenReady();
        SendJoinRequestWhenReady();
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
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForCodeRequest(
            code, _reclaimSecret() is { } secret ? SHA256.HashData(secret) : null)));
    }

    private void ReclaimWhenReconnected()
    {
        if (_host.Phase != HostingPhase.Hosting
            || !_hostAway()
            || _host.Code is not { } code
            || _reclaimSecret() is not { } secret
            || _reclaimSentOnThisLink
            || !_link.IsReadyToSend)
        {
            return;
        }

        _reclaimSentOnThisLink = true;
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForReclaim(code, secret)));
    }

    private void SendHelloWhenReady()
    {
        if (_join.Phase != JoinPhase.Contacting
            || _join.Code is not { } code
            || _joinerKeys() is not { } keys
            || string.Equals(_helloSentFor, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _helloSentFor = code.Value;
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForJoinHello(code, keys.PublicKey)));
    }

    private void SendJoinRequestWhenReady()
    {
        if (_join.Phase != JoinPhase.Contacting
            || _join.Code is not { } code
            || _join.HostPublicKey is not { } hostPublicKey
            || _joinerKeys() is not { } keys
            || string.Equals(_requestedJoinCode, code.Value, StringComparison.Ordinal)
            || !_link.IsReadyToSend)
        {
            return;
        }

        _requestedJoinCode = code.Value;

        byte[] key;
        try
        {
            key = keys.DeriveSharedKey(hostPublicKey, code);
        }
        catch (CryptographicException)
        {
            _join.Fail(SessionFailure.HostKeyUnusable);
            return;
        }

        var details = new JoinDetails
        {
            DisplayName = _joinDisplayName.WasStated ? _joinDisplayName.Value : null,
            ParticipantId = _claimedParticipantId?.ToString("D"),
        };

        var sealedDetails = JoinDetailsCodec.Seal(key, details, code, WireMessageType.JoinRequest);
        CryptographicOperations.ZeroMemory(key);

        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForJoinRequest(code, keys.PublicKey, sealedDetails)));
    }
}
