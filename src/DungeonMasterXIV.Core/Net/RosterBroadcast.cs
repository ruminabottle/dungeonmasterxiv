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

    private const int CatchUpBatchBytes = 24 * 1024;

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

        if (_audience.Recipients.Count == 0)
        {
            return;
        }

        SealToEveryRecipient(new SessionContent { Roster = Current() }, keys, code);
    }

    /// <summary>The host first, as Dungeon Master, then every admitted member.</summary>
    public List<RosterEntry> Current()
    {
        var roster = _audience.Recipients
            .Select(peer => new RosterEntry(peer.PeerCode.Value, peer.DisplayName.Value, peer.Role))
            .ToList();

        if (_host.OwnPeerCode() is { } ownCode)
        {
            roster.Insert(0, new RosterEntry(ownCode.Value, _host.Name().Value, SessionRole.DungeonMaster));
        }

        return roster;
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

        var associatedData = WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload);
        foreach (var batch in Batches(lines))
        {
            SealTo(peer, SessionContentCodec.Encode(new SessionContent { Entries = batch }), associatedData, keys, code);
        }
    }

    /// <summary>Splits catch-up lines so no sealed frame nears the relay's 64 KiB message limit.</summary>
    private static IEnumerable<List<StreamLine>> Batches(IReadOnlyList<StreamLine> lines)
    {
        var batch = new List<StreamLine>();
        var size = 0;

        foreach (var line in lines)
        {
            var lineSize = SessionContentCodec.Encode(new SessionContent { Entries = new[] { line } }).Length;
            if (batch.Count > 0 && size + lineSize > CatchUpBatchBytes)
            {
                yield return batch;
                batch = new List<StreamLine>();
                size = 0;
            }

            batch.Add(line);
            size += lineSize;
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
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
