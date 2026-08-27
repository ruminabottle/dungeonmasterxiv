using System;
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
    /// <returns>The derived session key if this drain admitted us, otherwise null.</returns>
    /// <remarks>
    /// A frame that does not parse is dropped rather than raised — anything can arrive from a relay
    /// and a malformed frame must not take the game client down. An unrecognised message type
    /// arrives as <see cref="WireMessageType.Unknown"/> from the deserializer and falls through
    /// without a handler needing to remember to ignore it (D-14).
    /// </remarks>
    public byte[]? Drain(JoinAttempt attempt, SessionKeyExchange? keys)
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
