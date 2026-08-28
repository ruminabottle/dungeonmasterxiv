using System;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What arrives from the host, and what it means for this client's own attempt to join.
/// </summary>
/// <remarks>
/// <para>
/// The inbound counterpart to <see cref="AdmissionAnnouncer"/>: that one owns what we say, this one
/// owns what we are told. Splitting them apart from <see cref="SessionCoordinator"/> leaves the
/// coordinator with the question it is actually for — whether a connection should exist.
/// </para>
/// <para>
/// <b>Frames are queued on arrival and applied on demand.</b> They arrive off the socket thread, and
/// mutating session state from a receive callback races the draw. Separating arrival from
/// application is what makes the ordering testable — a frame that changes nothing until the next
/// tick is an assertion rather than a hope.
/// </para>
/// </remarks>
public sealed class AdmissionInbox
{
    private readonly object _gate = new();
    private readonly Queue<byte[]> _frames = new();

    /// <summary>Takes a frame off the socket thread. Does not interpret it.</summary>
    public void Receive(byte[] frame)
    {
        lock (_gate)
        {
            _frames.Enqueue(frame);
        }
    }

    /// <summary>
    /// Applies everything that arrived since the last call to <paramref name="attempt"/>.
    /// </summary>
    /// <param name="attempt">This client's join attempt.</param>
    /// <param name="keys">This client's key pair, for deriving a session key on acceptance.</param>
    /// <param name="host">
    /// This client's hosting lifecycle, when it is a host. One socket carries both roles' traffic
    /// into one queue, so the relay's answer to a code request is drained here too rather than by a
    /// second consumer that would race this one for the same frames (BUG-36).
    /// </param>
    /// <param name="onJoinRequest">
    /// Called with the joiner's public key and self-declared name for each inbound
    /// <see cref="WireMessageType.JoinRequest"/>,
    /// when this client is a host. Null when there is nobody to tell, which is every joiner-only
    /// client (BUG-42).
    /// </param>
    /// <returns>The derived session key if this drain admitted us, otherwise null.</returns>
    /// <remarks>
    /// A frame that does not parse is dropped rather than raised — anything can arrive from a relay
    /// and a malformed frame must not take the game client down. An unrecognised message type
    /// arrives as <see cref="WireMessageType.Unknown"/> from the deserializer and falls through
    /// without a handler needing to remember to ignore it (D-14).
    /// </remarks>
    /// <param name="openWith">
    /// The shared key to open inbound content with, or null before one exists. A key derived during
    /// this same drain takes precedence — see the call site.
    /// </param>
    /// <param name="onContent">
    /// Called for each payload this client could open (D-11). Payloads sealed for somebody else are
    /// ordinary traffic and pass in silence.
    /// </param>
    public byte[]? Drain(
        JoinAttempt attempt,
        SessionKeyExchange? keys,
        HostSession? host = null,
        Action<byte[], DisplayName>? onJoinRequest = null,
        byte[]? openWith = null,
        Action<SessionContent>? onContent = null)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        byte[][] frames;
        lock (_gate)
        {
            frames = _frames.ToArray();
            _frames.Clear();
        }

        byte[]? sessionKey = null;

        foreach (var frame in frames)
        {
            if (!EnvelopeCodec.TryDecode(frame, out var envelope) || envelope is null)
            {
                continue;
            }

            // The relay's answer to this host's code request (R-1.2a). Registering is the one thing
            // a host waits on, and before BUG-36 nothing consumed these at all — the request was
            // never sent, so the answer never came and no handler was missed.
            if (host is not null && ApplyRegistration(envelope, host))
            {
                continue;
            }

            // Content from inside the session (D-11). Handled before the outcome arms for the same
            // reason JoinRequest is: a payload is not an outcome and matches none of them, so it
            // would fall through to nothing — the shape that cost BUG-42 an entire feature.
            //
            // A payload we cannot open is DISCARDED IN SILENCE, and that is correct rather than
            // lenient. Keys are pairwise, so the host seals one copy per participant and the relay
            // forwards every copy to every member: a client legitimately receives payloads sealed
            // for other people, all the time. Treating an unopenable payload as an error would make
            // ordinary traffic look like an attack.
            if (envelope.Type == WireMessageType.SessionPayload)
            {
                // The key derived EARLIER IN THIS DRAIN wins over the one we came in with. A
                // reconnecting client is admitted and sent the current roster in quick succession,
                // so JoinAccepted and the first payload can land in the same batch — and A-1.13a is
                // exactly the case that would silently show an empty list if the freshly derived
                // key were not used until the next frame arrived.
                ApplyContent(envelope, sessionKey ?? openWith, onContent);
                continue;
            }

            // A joiner asking to be let in. The consumer existed and was well tested from the day it
            // was written; nothing routed to it, so the relay forwarded every request to a host that
            // dropped it and no prompt was ever shown (BUG-42). Handled before the outcome arms
            // because a JoinRequest is not an outcome and matches none of them -- which is exactly
            // how it fell through to nothing.
            if (envelope.Type == WireMessageType.JoinRequest)
            {
                if (onJoinRequest is not null && envelope.PublicKey is { } joinerPublicKey)
                {
                    // Validated HERE rather than trusted, and a bad name does not drop the request:
                    // the person behind it is still waiting, and the prompt they need carries the
                    // fingerprint whatever the name turns out to be. See DisplayName.OrNone.
                    onJoinRequest(joinerPublicKey, DisplayName.OrNone(envelope.DisplayName));
                }

                continue;
            }

            // The relay refusing a code the JOINER asked for means no session is live under it —
            // in practice a mistyped code, which is the most common thing a joiner ever does. The
            // same message means something different to a host ("that code is taken, pick another"),
            // which is why this is a separate arm rather than a widened ApplyRegistration: one
            // function serving both readings is how the host's arm gets hijacked (BUG-43).
            if (envelope.Type == WireMessageType.CodeRefused && attempt.Phase == JoinPhase.Contacting)
            {
                attempt.Fail(SessionFailure.SessionCodeNotActive);
                continue;
            }

            // Pending notices first, and they are not outcomes. A pending notice says the DM is
            // looking; applying it is what gives this client something to compare while the
            // decision is still open (R-1.3a-i, A-1.3f-1).
            if (envelope.TryGetPendingHostKey() is { } hostPublicKey)
            {
                attempt.AwaitDecision(envelope.TryGetDeadline());

                if (keys is not null)
                {
                    attempt.HostKeyOffered(hostPublicKey, keys.PublicKey);
                }

                continue;
            }

            if (envelope.TryGetAdmissionOutcome() is { } outcome)
            {
                sessionKey = Apply(outcome, attempt, keys) ?? sessionKey;
            }
        }

        return sessionKey;
    }

    /// <summary>Empties the queue, for the end of a session.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _frames.Clear();
        }
    }

    /// <summary>
    /// Applies the relay's arbitration of a code request, or reports that this frame was not one.
    /// </summary>
    /// <remarks>
    /// R-1.2a: the host proposes and the relay arbitrates. A refusal means the code is already live,
    /// and the answer is to regenerate and ask again — never to surface it to the DM, who did not
    /// choose the code and can do nothing about the collision.
    /// </remarks>
    private static bool ApplyRegistration(WireEnvelope envelope, HostSession host)
    {
        // Only a host that is REGISTERING is waiting on one of these, and saying "handled" when it
        // is not was BUG-43: a JOINER's CodeRefused matched the arm below, was discarded by
        // CodeAlreadyLive's own phase guard, and the `return true` then stopped it ever reaching a
        // joiner arm. The frame was consumed by a branch that did nothing with it.
        if (host.Phase != HostingPhase.Registering)
        {
            return false;
        }

        switch (envelope.Type)
        {
            case WireMessageType.CodeAccepted:
                host.Registered();
                return true;

            case WireMessageType.CodeRefused:
                host.CodeAlreadyLive(SessionCodeGenerator.Next());
                return true;

            default:
                return false;
        }
    }

    /// <summary>Opens a payload if it is ours to open, and hands on what it said.</summary>
    private static void ApplyContent(WireEnvelope envelope, byte[]? key, Action<SessionContent>? onContent)
    {
        if (onContent is null || key is null || envelope.TryGetSealedPayload() is not { } sealedPayload)
        {
            return;
        }

        byte[] plaintext;
        try
        {
            plaintext = SessionCipher.Open(key, sealedPayload, envelope.AssociatedData());
        }
        catch (CryptographicException)
        {
            // Sealed for somebody else, or tampered with. Both are silence: see the call site.
            return;
        }

        if (SessionContentCodec.TryDecode(plaintext, out var content) && content is not null)
        {
            onContent(content);
        }
    }

    // Every outcome C6 defines is handled. Match takes a delegate per case, so omitting one is a
    // compile error rather than a branch that silently does nothing.
    private static byte[]? Apply(AdmissionOutcome outcome, JoinAttempt attempt, SessionKeyExchange? keys) =>
        outcome.Match(
            onAccepted: hostPublicKey =>
            {
                attempt.Admitted();
                return keys is not null && attempt.Code is { } code
                    ? keys.DeriveSharedKey(hostPublicKey, code)
                    : null;
            },
            onDenied: () =>
            {
                attempt.Denied();
                return (byte[]?)null;
            },
            onLapsed: () =>
            {
                attempt.Lapsed();
                return (byte[]?)null;
            });
}
