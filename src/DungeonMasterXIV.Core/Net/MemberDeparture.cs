using System;

namespace DungeonMasterXIV.Net;

/// <summary>Tells the host, over a sealed session payload, that this member is leaving.</summary>
internal sealed class MemberDeparture
{
    private readonly RelayLink _link;
    private readonly Func<SessionCode?> _code;
    private readonly Func<byte[]?> _sessionKey;

    public MemberDeparture(RelayLink link, Func<SessionCode?> code, Func<byte[]?> sessionKey)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(sessionKey);

        _link = link;
        _code = code;
        _sessionKey = sessionKey;
    }

    public bool Announce()
    {
        if (_code() is not { } code || _sessionKey() is not { } key)
        {
            return false;
        }

        var plaintext = SessionContentCodec.Encode(new SessionContent { Leaving = true });
        var sealedPayload = SessionCipher.Seal(
            key, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
        return true;
    }
}
