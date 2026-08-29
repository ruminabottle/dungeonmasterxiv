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
    /// <b>What it costs when it bites.</b> Measured on this machine, a valid join request costs
    /// ~0.44ms to drain, so eight is ~3.5ms — about a fifth of a 60fps frame. Two other harnesses
    /// measured the per-request cost lower (a 16.67ms frame filled by ~54 and ~55 requests against
    /// my ~38), which is the reason this is not tuned to any one of those numbers: they disagree by
    /// 40% across machines while all three agree the unbounded case is unbounded.
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

        // A bounded slice, FIFO, leaving the remainder queued (BUG-58). Taking the whole queue let
        // a stranger decide how much work this client did in one frame: the join path is open to
        // strangers by design, and ~38 valid requests filled a 60fps frame here.
        byte[][] frames;
        lock (_gate)
        {
            var taking = Math.Min(_frames.Count, FramesPerDrain);
            frames = new byte[taking][];

            for (var i = 0; i < taking; i++)
            {
                frames[i] = _frames.Dequeue();
            }
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
                ApplyContent(envelope, sessionKey ?? handlers.HostAuthored.OpenWith, handlers.HostAuthored.OnContent, log);

                // THE HOST'S SIDE OF THE SAME FRAME (R-1.3k, DMXENG-50). Both arms run, and only
                // one of them can ever fire: the line above opens HOST-authored content with the
                // key a joiner derived on admission, and this one opens MEMBER-authored content
                // with the keys a host shares with its peers. A payload is sealed under exactly one
                // of those, so the other simply finds nothing to do.
                //
                // NOT AN `else`, DELIBERATELY. An else would make the arms exclusive by control
                // flow, and the property that makes them exclusive is the SEAL — one key opens a
                // payload and the rest cannot. Writing it as an else would hide a real question
                // (what if a client is both?) behind a branch that answers it by accident.
                MemberContentReader.Apply(envelope, handlers, log);
                continue;
            }

            // A joiner asking to be let in. The consumer existed and was well tested from the day it
            // was written; nothing routed to it, so the relay forwarded every request to a host that
            // dropped it and no prompt was ever shown (BUG-42). Handled before the outcome arms
            // because a JoinRequest is not an outcome and matches none of them -- which is exactly
            // how it fell through to nothing.
            if (envelope.Type == WireMessageType.JoinRequest)
            {
                // THE KEY IS CHECKED HERE, AT THE ONE DOOR IT ARRIVES THROUGH (BUG-56). A joiner
                // controls these bytes and nothing validated them, so a peer the host could never
                // derive a key for could be admitted: addressable by the relay, unreachable by the
                // host, and silent to everyone. Guarding each place that derives instead is a
                // denylist of the call sites that happen to exist today, and the next one is
                // unprotected — which is why this is at the boundary and not beside the crypto.
                //
                // A refused request is DROPPED, exactly as any frame that does not parse is dropped
                // a few lines above. That is the existing rule for unusable input on this path, not
                // a new answer to what the DM should be told about it — that remains a product
                // question (D-8) and is deliberately left open.
                if (handlers.Admission.OnJoinRequest is { } onJoinRequest
                    && envelope.PublicKey is { } joinerPublicKey
                    && SessionKeyExchange.CanAgreeWith(joinerPublicKey))
                {
                    // Validated HERE rather than trusted, and a bad name does not drop the request:
                    // the person behind it is still waiting, and the prompt they need carries the
                    // fingerprint whatever the name turns out to be. See DisplayName.OrNone.
                    // The claim travels as the RAW STRING it arrived as and is resolved by the
                    // host (T-37) -- unvalidated here on purpose, because nothing is granted on it
                    // and CampaignRelink.Resolve is where it meets a parse and a roster. See
                    // JoinerAdmission.OnJoinRequest.
                    onJoinRequest(
                        joinerPublicKey,
                        DisplayName.OrNone(envelope.DisplayName),
                        envelope.ClaimedParticipantId);
                }

                continue;
            }

            // THE HOP THAT DID NOT EXIST (BUG-75). The joiner SENDS this (OutboundHandshake), the
            // relay ROUTES it to the host (RelayRouter), and until now nothing here consumed it --
            // so it reached the host and fell through to nothing. Sent, routed, silently dropped:
            // the same shape as BUG-42's consumer nothing routed to, arriving from the other side.
            //
            // Handled BEFORE the outcome arms for the same reason JoinRequest is: a receipt is not
            // an outcome and matches none of them, which is exactly how it fell through.
            //
            // ESTABLISHES STATE 1 ONLY (R-1.3a-iv). It reports that the joiner HELD THE HOST KEY and
            // could render a fingerprint -- a CAPABILITY, never a claim that a human compared
            // anything. R-1.3a-iii forbids the second: an acknowledgement of the human act rides the
            // channel an attacker controls, so it is forgeable exactly when it matters.
            if (envelope.Type == WireMessageType.JoinerHoldsFingerprint)
            {
                if (handlers.Admission.OnComparabilityReceipt is { } onReceipt
                    && envelope.TryGetFingerprintReceiptKey() is { } receiptKey)
                {
                    onReceipt(receiptKey);
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

            if (envelope.TryGetAdmissionOutcome(keys?.PublicKey) is { } outcome)
            {
                // The participant id rides the SAME envelope as the outcome and is read from it
                // here rather than folded into AdmissionOutcome. It decides nothing about the
                // admission, and a consumer that could read it through Match would be reading an
                // identity as an answer -- the same separation TryGetFingerprintReceiptKey keeps.
                sessionKey = Apply(
                    outcome, attempt, keys, ParticipantReceipt.TryRead(envelope, keys?.PublicKey)) ?? sessionKey;
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
    private static void ApplyContent(
        WireEnvelope envelope,
        byte[]? key,
        Action<SessionContent>? onContent,
        ISessionTransportLog? log)
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

        // PR #86 FINDING 4, AND IT IS PLACED HERE RATHER THAN INSIDE TryDecode ON PURPOSE.
        // The distinction the finding rests on is only knowable at THIS call site: Open SUCCEEDED
        // just above, so the AEAD authenticated and this payload was sealed for us by a keyholder.
        // A decode failure after that point can never be "traffic for somebody else" -- it is
        // version skew or an encoding defect, and both are faults worth a line. Inside TryDecode
        // that context is gone: its other callers decode plaintext of unproven provenance, and a
        // log line there would fire on inputs where silence is correct.
        //
        // It costs nothing on the normal path because it cannot fire there.
        if (!SessionContentCodec.TryDecode(plaintext, out var content, log) || content is null)
        {
            log?.Warning(
                "A session payload authenticated and then failed to decode. It was sealed for this "
                + "client by a keyholder, so this is version skew or an encoding defect rather than "
                + "traffic for somebody else. The payload was discarded.");
            return;
        }

        onContent(content);
    }


    // Every outcome C6 defines is handled. Match takes a delegate per case, so omitting one is a
    // compile error rather than a branch that silently does nothing.
    private static byte[]? Apply(
        AdmissionOutcome outcome,
        JoinAttempt attempt,
        SessionKeyExchange? keys,
        Guid? participantId) =>
        outcome.Match(
            onAccepted: hostPublicKey =>
            {
                // BUG-59, AND THE GUARD IS BEFORE Admitted() ON PURPOSE. The host's key is as
                // untrusted as the joiner's was in BUG-56, and it reaches here by controlling the
                // RELAY — the position D-11 assumes an attacker may occupy. Guarding the derive
                // alone was measured and is wrong: Admitted() would still run, leaving
                // Phase=Admitted with a null SessionKey and MayReceiveSessionState true, which is
                // the silently-unreachable participant BUG-56 exists to remove, rebuilt here.
                //
                // Failing rather than dropping is a ruling, not a default. Dropping cannot be
                // neutral because NOTHING LAPSES A JOINER LOCALLY: the only Lapsed() call is the
                // arm below, driven by the host, and a host that sent an acceptance believes this
                // client is in and never sends one. A dropped acceptance leaves the joiner in
                // AwaitingDecision showing a dead countdown indefinitely — A-1.5j applied to UI
                // state, which is why this reports instead.
                if (!SessionKeyExchange.CanAgreeWith(hostPublicKey))
                {
                    attempt.Fail(SessionFailure.HostKeyUnusable);
                    return null;
                }

                attempt.Admitted();

                // AFTER Admitted(), never before, and ToldItIsParticipant guards the phase itself
                // so the ordering is stated in two places on purpose. R-1.3b: an unadmitted client
                // is entitled to nothing, and the guard above can fail this attempt before here.
                if (participantId is { } told)
                {
                    attempt.ToldItIsParticipant(told);
                }

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
