using System;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What arrives from the rest of the session, and what this client does about it.
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
    /// <summary>
    /// How many queued frames one <c>Drain</c> may process. The rest wait for the next tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Eight because that is a full FFXIV party</b> — the largest number of people who could
    /// legitimately ask to join in the same frame. Below it, an ordinary full-party join would be
    /// deferred for no reason; far above it, the bound stops doing its job. It is sized to the
    /// product's own unit rather than to a millisecond figure, because the millisecond figure moves
    /// between machines and the party does not.
    /// </para>
    /// <para>
    /// <b>What it costs when it bites, to an order of magnitude and deliberately no further.</b>
    /// Draining a valid join request is well under a millisecond here, so a full drain of eight is a
    /// small fraction of a 16.67ms frame. No precise per-frame count is stated, because every precise
    /// count this file has carried has been wrong.
    /// </para>
    /// <para>
    /// <b>The earlier figure was an artefact, and its supporting arithmetic was an artefact of the
    /// artefact.</b> This paragraph used to read "~0.44ms, so eight is ~3.5ms" and "~38 requests per
    /// frame against ~54 and ~55, so they disagree by 40%". The harness timed ECDH key generation
    /// and the public-key export INSIDE the loop, so it measured making a joiner rather than
    /// draining its request. The "40%" was then arithmetically correct given that bad input — which
    /// is exactly what made it read as verified rather than as derived.
    /// </para>
    /// <para>
    /// <b>The measurements disagree too much to tune to, and that is the durable part.</b> A
    /// corrected harness — every frame built outside the timed region, the key-agreement arm proven
    /// reached on all 24000 requests rather than assumed — puts a 16.67ms frame at roughly 70
    /// requests. Two other harnesses put it at ~54 and ~55. An earlier corrected run of my own put
    /// it near 50. That is a spread of about 40% between two runs of the SAME harness on the SAME
    /// machine, which I cannot account for and am not going to paper over. It is the reason the
    /// bound is sized to a full FFXIV party, which does not move between machines, rather than to
    /// any millisecond figure, which plainly does.
    /// </para>
    /// <para>
    /// <b>This defers; it refuses nobody.</b> Every frame is still processed, in order, on a later
    /// tick — which is why bounding here needed no product decision, and why capping
    /// <c>AdmissionDesk</c>'s pending list would have (BUG-58): that one decides what a legitimate
    /// joiner is told when they arrive at the cap.
    /// </para>
    /// </remarks>
    private const int FramesPerDrain = 8;

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
    /// <param name="handlers">
    /// What this client does with what arrives — see <see cref="InboundHandlers"/>. Omitting it
    /// drains without acting, which is what a caller that only wants the derived key wants.
    /// </param>
    /// <param name="log">
    /// Where this drain reports content it accepted but had to strip — see
    /// <see cref="SessionContentCodec.TryDecode"/>. Optional because a caller that only wants
    /// the derived key has nobody to tell; a null log makes the strip silent, which is the
    /// condition BUG-70 was about rather than an accepted default.
    /// </param>
    /// <returns>The derived session key if this drain admitted us, otherwise null.</returns>
    /// <remarks>
    /// A frame that does not parse is dropped rather than raised — anything can arrive from a relay
    /// and a malformed frame must not take the game client down. An unrecognised message type
    /// arrives as <see cref="WireMessageType.Unknown"/> from the deserializer and falls through
    /// without a handler needing to remember to ignore it (D-14).
    /// </remarks>
    public byte[]? Drain(
        JoinAttempt attempt,
        SessionKeyExchange? keys,
        HostSession? host = null,
        InboundHandlers handlers = default,
        ISessionTransportLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var arriving = new InboundFrame(attempt, keys, host, handlers, log);
        byte[]? sessionKey = null;

        foreach (var frame in TakeSlice())
        {
            if (!EnvelopeCodec.TryDecode(frame, out var envelope) || envelope is null)
            {
                continue;
            }

            // THE KEY DERIVED EARLIER IN THIS DRAIN IS CARRIED FORWARD, and that is A-1.13a. A
            // reconnecting client is admitted and sent the current roster in quick succession, so
            // JoinAccepted and the first payload can land in the same batch -- and the roster would
            // silently render empty if the freshly derived key were not used until the next frame.
            sessionKey = arriving.Apply(envelope, sessionKey);
        }

        return sessionKey;
    }

    /// <summary>A bounded FIFO slice of what has arrived, leaving the remainder queued (BUG-58).</summary>
    /// <remarks>
    /// Taking the whole queue let a stranger decide how much work this client did in one frame: the
    /// join path is open to strangers by design. Draining one costs key agreement; see
    /// <c>FramesPerDrain</c> for why no per-frame count is quoted. This DEFERS and refuses nobody --
    /// the remainder stays queued, in order, for a later tick.
    /// </remarks>
    private byte[][] TakeSlice()
    {
        lock (_gate)
        {
            var taking = Math.Min(_frames.Count, FramesPerDrain);
            var frames = new byte[taking][];

            for (var i = 0; i < taking; i++)
            {
                frames[i] = _frames.Dequeue();
            }

            return frames;
        }
    }

    /// <summary>Empties the queue, for the end of a session.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _frames.Clear();
        }
    }

}
