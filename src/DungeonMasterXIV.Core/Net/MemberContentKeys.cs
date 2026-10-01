using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>A member's peer code paired with the key the host shares with that member.</summary>
public readonly record struct PeerContentKey(PeerCode Peer, byte[] Key);

/// <summary>Derives and caches the host's shared key with each admitted member, clearing them when the session moves.</summary>
internal sealed class MemberContentKeys
{
    private readonly SessionAudience _audience;
    private readonly Func<SessionKeyExchange?> _hostKeys;
    private readonly Func<SessionCode?> _hostCode;
    private readonly ISessionTransportLog _log;

    private readonly Dictionary<string, byte[]> _derived = new(StringComparer.Ordinal);

    private SessionKeyExchange? _derivedWith;
    private string? _derivedFor;

    public MemberContentKeys(
        SessionAudience audience,
        Func<SessionKeyExchange?> hostKeys,
        Func<SessionCode?> hostCode,
        ISessionTransportLog log)
    {
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(hostKeys);
        ArgumentNullException.ThrowIfNull(hostCode);
        ArgumentNullException.ThrowIfNull(log);

        _audience = audience;
        _hostKeys = hostKeys;
        _hostCode = hostCode;
        _log = log;
    }

    public IEnumerable<PeerContentKey> Candidates()
    {
        if (_hostKeys() is not { } keys || _hostCode() is not { } code)
        {
            yield break;
        }

        ForgetIfTheSessionMoved(keys, code);

        foreach (var peer in _audience.Recipients)
        {
            if (peer.PublicKey is not { } peerKey)
            {
                continue;
            }

            var forPeer = Convert.ToHexString(peerKey);

            if (!_derived.TryGetValue(forPeer, out var shared))
            {
                if (!TryDerive(keys, peerKey, code, peer.PeerCode, out shared))
                {
                    continue;
                }

                _derived[forPeer] = shared;
            }

            yield return new PeerContentKey(peer.PeerCode, shared);
        }
    }

    public void Forget()
    {
        foreach (var key in _derived.Values)
        {
            CryptographicOperations.ZeroMemory(key);
        }

        _derived.Clear();
        _derivedWith = null;
        _derivedFor = null;
    }

    private void ForgetIfTheSessionMoved(SessionKeyExchange keys, SessionCode code)
    {
        if (ReferenceEquals(_derivedWith, keys) && string.Equals(_derivedFor, code.Value, StringComparison.Ordinal))
        {
            return;
        }

        Forget();
        _derivedWith = keys;
        _derivedFor = code.Value;
    }

    private bool TryDerive(
        SessionKeyExchange keys,
        byte[] peerKey,
        SessionCode code,
        PeerCode peerCode,
        out byte[] shared)
    {
        try
        {
            shared = keys.DeriveSharedKey(peerKey, code);
            return true;
        }
        catch (CryptographicException exception)
        {
            _log.Warning(
                exception,
                $"Cannot open content from participant {peerCode.Value}: their public key will not "
                + "import, so no shared key can be derived. They remain admitted, and anything they "
                + "send will be discarded unopened.");

            shared = [];
            return false;
        }
    }
}
