namespace DungeonMasterXIV.Net;

/// <summary>
/// One decoded frame, and what this client does about it (DMXENG-97).
/// </summary>
/// <remarks>
/// <para>
/// <b>EXTRACTED WITHOUT CHANGING ANYTHING IT DOES.</b> Every arm below, and every word of reasoning
/// attached to it, was lifted verbatim out of <see cref="AdmissionInbox.Drain"/>, which stood at 180
/// lines against a block of 60. The only edits were a dedent and turning each <c>continue</c> into
/// <c>return sessionKey</c> — the same statement in a method that the loop's <c>continue</c> was in a
/// loop. Nothing was reordered, renamed, merged or reworded.
/// </para>
/// <para>
/// <b>THE ARM ORDER IS LOAD-BEARING AND IS NOT AN ACCIDENT OF LAYOUT.</b> Payload, join request and
/// receipt are all handled BEFORE the outcome arms because none of them is an outcome and each would
/// otherwise fall through to nothing — the shape that cost BUG-42 an entire feature and BUG-75 a hop.
/// Reordering these is a behaviour change wearing a tidy-up's clothes.
/// </para>
/// <para>
/// <b>Why a record struct rather than seven parameters.</b> The arms need five pieces of context and
/// the running key. Passing all six to a static helper would have put a 7-parameter method in a file
/// that already flags <c>Drain</c> at 5. Carrying the five as a value and deconstructing them back
/// into their original names is what let the body move without a single identifier changing — so a
/// reviewer can diff the moved text against the old <c>Drain</c> and see that it is the same text.
/// </para>
/// </remarks>
/// <param name="Attempt">The join this client is making, if it is joining.</param>
/// <param name="Keys">This client's ephemeral keys, if it has them yet.</param>
/// <param name="Host">The session this client is hosting, if it is hosting.</param>
/// <param name="Handlers">Where each kind of arriving frame is delivered.</param>
/// <param name="Log">Where transport-level notes go, if anywhere.</param>
internal readonly record struct InboundFrame(
    JoinAttempt Attempt,
    SessionKeyExchange? Keys,
    HostSession? Host,
    InboundHandlers Handlers,
    ISessionTransportLog? Log)
{
    /// <summary>Applies one decoded frame, returning the session key as it now stands.</summary>
    /// <remarks>
    /// <para>
    /// <b>THE ORDER OF THESE ARMS IS THE BEHAVIOUR, NOT THE LAYOUT.</b> Payload, join request and
    /// receipt are each handled BEFORE the outcome arms because none of them is an outcome and each
    /// would otherwise fall through to nothing — the shape that cost BUG-42 an entire feature and
    /// BUG-75 a hop. Re-ordering this list is a behaviour change wearing a tidy-up's clothes.
    /// </para>
    /// <para>
    /// The key derived earlier in the same drain is carried in and out rather than held as state, so
    /// a caller draining several frames gets the ordering A-1.13a depends on without this type
    /// needing a lifetime. Returning it unchanged is how an arm says "handled, nothing to add".
    /// </para>
    /// </remarks>
    /// <param name="envelope">The frame, already decoded.</param>
    /// <param name="sessionKey">The key as of the previous frame, or null.</param>
    internal byte[]? Apply(WireEnvelope envelope, byte[]? sessionKey)
    {
        var host = Host;

        // The relay's answer to this host's code request (R-1.2a). Registering is the one thing
        // a host waits on, and before BUG-36 nothing consumed these at all — the request was
        // never sent, so the answer never came and no handler was missed.
        if (host is not null && InboundApplication.ApplyRegistration(envelope, host))
        {
            return sessionKey;
        }

        if (TryContent(envelope, sessionKey)
            || TryJoinRequest(envelope)
            || TryConnectionDropped(envelope)
            || TryFingerprintReceipt(envelope)
            || TryCodeRefused(envelope)
            || TryPendingNotice(envelope))
        {
            return sessionKey;
        }

        return ApplyOutcome(envelope, sessionKey);
    }

    private bool TryContent(WireEnvelope envelope, byte[]? sessionKey)
    {
        var handlers = Handlers;
        var log = Log;

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
            InboundApplication.ApplyContent(envelope, sessionKey ?? handlers.HostAuthored.OpenWith, handlers.HostAuthored.OnContent, log);

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
            return true;
        }

        return false;
    }

    private bool TryJoinRequest(WireEnvelope envelope)
    {
        var handlers = Handlers;

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

            return true;
        }

        return false;
    }

    private bool TryConnectionDropped(WireEnvelope envelope)
    {
        var handlers = Handlers;

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
        // A-1.28. The door delivers through itself; the reasoning is on TransportNotices.
        if (envelope.Type == WireMessageType.ConnectionDropped)
        {
            handlers.Transport.Deliver(envelope);
            return true;
        }

        return false;
    }

    private bool TryFingerprintReceipt(WireEnvelope envelope)
    {
        var handlers = Handlers;

        if (envelope.Type == WireMessageType.JoinerHoldsFingerprint)
        {
            if (handlers.Admission.OnComparabilityReceipt is { } onReceipt
                && envelope.TryGetFingerprintReceiptKey() is { } receiptKey)
            {
                onReceipt(receiptKey);
            }

            return true;
        }

        return false;
    }

    private bool TryCodeRefused(WireEnvelope envelope)
    {
        var attempt = Attempt;

        // The relay refusing a code the JOINER asked for means no session is live under it —
        // in practice a mistyped code, which is the most common thing a joiner ever does. The
        // same message means something different to a host ("that code is taken, pick another"),
        // which is why this is a separate arm rather than a widened ApplyRegistration: one
        // function serving both readings is how the host's arm gets hijacked (BUG-43).
        if (envelope.Type == WireMessageType.CodeRefused && attempt.Phase == JoinPhase.Contacting)
        {
            attempt.Fail(SessionFailure.SessionCodeNotActive);
            return true;
        }

        return false;
    }

    private bool TryPendingNotice(WireEnvelope envelope)
    {
        var attempt = Attempt;
        var keys = Keys;

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

            return true;
        }

        return false;
    }

    private byte[]? ApplyOutcome(WireEnvelope envelope, byte[]? sessionKey)
    {
        var attempt = Attempt;
        var keys = Keys;

        if (envelope.TryGetAdmissionOutcome(keys?.PublicKey) is { } outcome)
        {
            // The participant id rides the SAME envelope as the outcome and is read from it
            // here rather than folded into AdmissionOutcome. It decides nothing about the
            // admission, and a consumer that could read it through Match would be reading an
            // identity as an answer -- the same separation TryGetFingerprintReceiptKey keeps.
            sessionKey = InboundApplication.Apply(
                outcome, attempt, keys, ParticipantReceipt.TryRead(envelope, keys?.PublicKey)) ?? sessionKey;
        }

        return sessionKey;
    }
}
