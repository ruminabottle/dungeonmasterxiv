using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>Seals the roster, closing notices and stream lines to each admitted member and sends them.</summary>
internal sealed class RosterBroadcast
{
    private readonly RelayLink _link;
    private readonly SessionAudience _audience;
    private readonly HostIdentity _host;
    private readonly ISessionTransportLog _log;

    public RosterBroadcast(
        RelayLink link,
        SessionAudience audience,
        HostIdentity host,
        ISessionTransportLog log)
    {
        _link = link;
        _audience = audience;
        _host = host;
        _log = log;
    }

    public void Publish()
    {
        if (_host.Keys() is not { } keys || _host.Code() is not { } code || !_link.IsReadyToSend)
        {
            return;
        }

        var roster = _audience.Recipients
            .Select(peer => new RosterEntry(peer.PeerCode.Value, peer.DisplayName.Value, peer.Role))
            .ToList();

        if (roster.Count == 0)
        {
            return;
        }

        if (_host.OwnPeerCode() is { } ownCode)
        {
            roster.Insert(0, new RosterEntry(ownCode.Value, _host.Name().Value, SessionRole.DungeonMaster));
        }

        SealToEveryRecipient(new SessionContent { Roster = roster }, keys, code);
    }

    public void PublishClosing(SessionClosing closing)
    {
        if (_host.Keys() is not { } keys || _host.Code() is not { } code || !_link.IsReadyToSend)
        {
            return;
        }

        SealToEveryRecipient(new SessionContent { ClosingAtUtcTicks = closing.UtcTicks }, keys, code);
    }

    public void PublishEntry(StreamLine line)
    {
        if (_host.Keys() is not { } keys || _host.Code() is not { } code || !_link.IsReadyToSend)
        {
            return;
        }

        SealToEveryRecipient(new SessionContent { Entries = new[] { line } }, keys, code);
    }

    public void PublishEntriesTo(PeerCode recipient, IReadOnlyList<StreamLine> lines)
    {
        if (lines.Count == 0
            || _host.Keys() is not { } keys
            || _host.Code() is not { } code
            || !_link.IsReadyToSend
            || _audience.Find(recipient) is not { } peer)
        {
            return;
        }

        var plaintext = SessionContentCodec.Encode(new SessionContent { Entries = lines });
        SealTo(peer, plaintext, WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload), keys, code);
    }

    private void SealToEveryRecipient(SessionContent content, SessionKeyExchange keys, SessionCode code)
    {
        var plaintext = SessionContentCodec.Encode(content);
        var associatedData = WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload);

        foreach (var peer in _audience.Recipients)
        {
            SealTo(peer, plaintext, associatedData, keys, code);
        }
    }

    private void SealTo(AdmittedPeer peer, byte[] plaintext, byte[] associatedData, SessionKeyExchange keys, SessionCode code)
    {
        if (peer.PublicKey is not { } peerKey)
        {
            _log.Warning(
                $"Roster broadcast skipped participant {peer.PeerCode.Value}: no public key, so the "
                + "host cannot address them. They remain admitted and will hear nothing from this "
                + "or any later broadcast.");
            return;
        }

        byte[] shared;
        try
        {
            shared = keys.DeriveSharedKey(peerKey, code);
        }
        catch (CryptographicException exception)
        {
            _log.Warning(
                exception,
                $"Roster broadcast skipped participant {peer.PeerCode.Value}: their public key "
                + "will not import, so no shared key can be derived. They remain admitted and "
                + "will hear nothing from this or any later broadcast.");
            return;
        }

        var sealedPayload = SessionCipher.Seal(shared, plaintext, associatedData);
        _link.Send(EnvelopeCodec.Encode(WireEnvelope.ForSessionPayload(code, sealedPayload)));
    }
}
