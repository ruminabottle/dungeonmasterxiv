using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// One candidate for opening member-authored content: a peer, and the key shared with them.
/// </summary>
/// <remarks>
/// The pair travels together because the key is what establishes the peer. Handing back a bare
/// <c>byte[]</c> would open the payload and lose the only answer to <i>whose event was that</i> —
/// which A-2.5 needs by name, since it orders the roll log by host receipt order and a roll with no
/// author is not a roll.
/// </remarks>
/// <param name="Peer">The participant this key is shared with (A-1.2d).</param>
/// <param name="Key">The shared key, from <see cref="SessionKeyExchange.DeriveSharedKey"/>.</param>
public readonly record struct PeerContentKey(PeerCode Peer, byte[] Key);

/// <summary>
/// The keys a host can open member-authored content with — one per admitted peer (R-1.3k).
/// </summary>
/// <remarks>
/// <para>
/// <b>The inbound counterpart to <see cref="RosterBroadcast"/>'s sealing loop, and deliberately the
/// same derivation.</b> That one derives a key per recipient to seal outbound; this derives the
/// same key per peer to open inbound. Keys are pairwise, so a host holds a different shared secret
/// with every participant and there is no single key that reaches the room —
/// <c>AdmissionInbox</c> tried one, which is the whole of the defect R-1.3k names.
/// </para>
/// <para>
/// <b>WHY TRIAL DECRYPTION RATHER THAN SELECTION, WHICH IS A MEASURED CHOICE AND NOT A TASTE.</b>
/// Nothing on the wire says who sent a payload — <see cref="WireEnvelope.ForSessionPayload"/> sets
/// only the nonce and the ciphertext — and the relay forwards it unmodified, so there is nothing to
/// select on. A sender field would put a peer identifier in the clear where the relay reads it
/// (D-8), would be forgeable by the one party who benefits from forging it, and would still have to
/// be confirmed by opening. So the key that opens a payload IS the identification, and the AEAD tag
/// is what makes that sound: a wrong key fails, it does not quietly produce different plaintext.
/// </para>
/// <para>
/// <b>WHY THE CACHE IS LOAD-BEARING RATHER THAN AN OPTIMISATION.</b> Measured on the machine this
/// was written on: <see cref="SessionKeyExchange.DeriveSharedKey"/> costs <b>~416µs</b> and a failed
/// <see cref="SessionCipher.Open"/> costs <b>~4.9µs</b> — derivation is roughly eighty times a
/// trial. <c>AdmissionInbox</c> drains up to eight frames per tick and a full FFXIV party is eight
/// peers, so deriving per candidate per payload is <b>8 × 8 × 416µs ≈ 26.6ms from a single
/// drain — more than an entire 60fps frame</b>, which would be a worse defect than the one being
/// fixed. Deriving once per peer brings the same worst case to ~314µs, about 2% of a frame, against
/// a one-off ~3.3ms spread across the session. <b>Without the cache this mechanism is not
/// admissible; with it, it is unremarkable.</b>
/// </para>
/// <para>
/// <b>Derived lazily rather than at admission</b>, because admission is on the path a stranger can
/// drive: a peer that is admitted and never speaks should not cost 416µs of the DM's frame, and
/// deriving at the first payload puts the cost where the traffic is.
/// </para>
/// <para>
/// <b>NO PRODUCTION CODE SENDS MEMBER-AUTHORED CONTENT YET, AND SAYING SO IS PART OF THIS TYPE.</b>
/// <see cref="WireEnvelope.ForSessionPayload"/> has exactly one production caller —
/// <c>RosterBroadcast</c>, the host — so today nothing on a member client ever produces a payload
/// for this to open. The relay <i>will</i> carry one: <c>RelayRouter.ForwardPayload</c> routes from
/// any admitted member to <c>MembersExcept(sender)</c>. <b>The sending half is DMXENG-11 / A-1.15, a
/// live ticket held by another engineer and blocked on this one.</b> This is a sequence, not half a
/// wire — but until that lands, <b>this is a capability the product has and does not yet use</b>,
/// and a reader who takes it for shipped behaviour has been misled. A model with no production
/// caller is not a shipped behaviour.
/// </para>
/// </remarks>
internal sealed class MemberContentKeys
{
    private readonly SessionAudience _audience;
    private readonly Func<SessionKeyExchange?> _hostKeys;
    private readonly Func<SessionCode?> _hostCode;
    private readonly ISessionTransportLog _log;

    /// <summary>Derived keys by peer public key, hex-encoded. See the type's remarks for why.</summary>
    private readonly Dictionary<string, byte[]> _derived = new(StringComparer.Ordinal);

    private SessionKeyExchange? _derivedWith;
    private string? _derivedFor;

    /// <summary>Wires the candidate source to the host state it reads.</summary>
    /// <param name="audience">Who is admitted, and the public keys to reach them.</param>
    /// <param name="hostKeys">The host's ephemeral keys, read at use time rather than captured.</param>
    /// <param name="hostCode">The session being hosted, or null when not hosting.</param>
    /// <param name="log">
    /// Where a peer whose key will not import is reported. <b>Required, not optional</b>, for the
    /// reason <see cref="RosterBroadcast"/> states: an optional log is one the single production
    /// caller can omit and nothing fails (DMXENG-13).
    /// </param>
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

    /// <summary>
    /// Every key this host could open member-authored content with, one per admitted peer.
    /// </summary>
    /// <remarks>
    /// Empty when not hosting, which is what makes this safe to hand to a joiner-only client: there
    /// are no candidates, so the member arm does nothing rather than needing a role check.
    /// </remarks>
    public IEnumerable<PeerContentKey> Candidates()
    {
        if (_hostKeys() is not { } keys || _hostCode() is not { } code)
        {
            yield break;
        }

        ForgetIfTheSessionMoved(keys, code);

        foreach (var peer in _audience.Recipients)
        {
            // A peer with no key is one the host cannot speak to OR hear from. It cannot arise on
            // the production path — AdmissionControl takes the key off the request it is answering —
            // and RosterBroadcast reports the same case on the outbound side. Reported once there is
            // enough: the two loops run over the same list, so warning here as well would double
            // every line for a case that cannot happen.
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

    /// <summary>
    /// Drops every derived key, zeroing it. For the end of a session.
    /// </summary>
    /// <remarks>
    /// <b>Zeroed rather than dropped, because these are session keys and D-8 is about what outlives
    /// a session.</b> The host's key pair is disposed when hosting stops; a cache of keys derived
    /// from it that stayed live in the heap afterwards would undo that at one remove.
    /// </remarks>
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

    /// <summary>
    /// Invalidates the cache when it belongs to a different session than the one now running.
    /// </summary>
    /// <remarks>
    /// A derived key is a function of the host key pair, the peer's key and the session code. The
    /// peer's key is the cache key; the other two are per-session, so they are checked here.
    /// <b>Both, not just the code:</b> R-1.2a lets a host regenerate its code on a collision without
    /// new keys, and a host that restarts gets new keys under a code it could in principle draw
    /// again. Either alone leaves a case where a stale key is served under a fresh session.
    /// </remarks>
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
            // A key that will not import. Nothing validates that a joiner's public key is a
            // well-formed SPKI blob at every door it can arrive through, so a client sending
            // arbitrary bytes reaches here. Skipping that peer keeps the host hearing everyone
            // else; throwing would let any joiner deafen the DM by sending rubbish, through the one
            // path that is open to strangers by design.
            //
            // The peer CODE is in the message because it is the only thing that identifies which
            // person it is (A-1.2d); the display name would not, and D-8 keeps a character name out
            // of a log entirely.
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
