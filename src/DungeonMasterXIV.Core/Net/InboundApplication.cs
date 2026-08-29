using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What a decoded frame DOES to this client's own state — the applying half of the inbound path.
/// </summary>
/// <remarks>
/// <para>
/// <b>The seam is STATE, and it was already here before the size limit found it.</b>
/// <see cref="AdmissionInbox"/> owns a lock and a queue: frames arrive, wait, and are handed out in
/// bounded slices. All three methods below were ALREADY <c>private static</c> — they touch no field,
/// take everything they need as parameters, and answer a different question: <i>given this frame,
/// what changes?</i> <b>Moving them cannot change behaviour because they never had state to leave
/// behind</b>, which is why this extraction is a move rather than a redesign.
/// </para>
/// <para>
/// <b>WHY NOT <c>Drain</c> — the decision, stated because the ticket asked for it rather than for
/// the result.</b> <c>Drain</c> is 173 lines against a 60-line method block and is the obvious
/// candidate. <b>It is also BUG-103's largest entry, and BUG-87 is held on it.</b> Splitting it here
/// would resolve a <c>lane-bug</c> item inside a <c>lane-feature</c> chunk — no QA triage, no
/// breakfix owner — and would incidentally unblock a second one.
/// <b>THIS EXTRACTION ADDRESSES NEITHER. <c>Drain</c>'s length is unchanged.</b>
/// </para>
/// <para>
/// <b>And it did not have to.</b> Moving these three frees 142 lines where DMXENG-58 needs 7, so the
/// bug-lane region never had to be touched to unblock the feature lane. <b>That the boundary-
/// respecting cut was also the sufficient one is luck, not design</b> — had it not been, the answer
/// would have been to report that and stop, rather than to cross the lane quietly.
/// </para>
/// </remarks>
internal static class InboundApplication
{
    /// <summary>
    /// Applies the relay's arbitration of a code request, or reports that this frame was not one.
    /// </summary>
    /// <remarks>
    /// R-1.2a: the host proposes and the relay arbitrates. A refusal means the code is already live,
    /// and the answer is to regenerate and ask again — never to surface it to the DM, who did not
    /// choose the code and can do nothing about the collision.
    /// </remarks>
    internal static bool ApplyRegistration(WireEnvelope envelope, HostSession host)
    {
        // Only a host that is REGISTERING is waiting on one of these, and saying "handled" when it
        // is not was BUG-43: a JOINER's CodeRefused matched the arm below, was discarded by
        // CodeAlreadyLive's own phase guard, and the `return true` then stopped it ever reaching a
        // joiner arm. The frame was consumed by a branch that did nothing with it.
        if (host.Phase != HostingPhase.Registering)
        {
            return false;
        }

        // BUG-89: THE ANSWER MUST NAME THE CODE THIS HOST ASKED ABOUT. The phase alone does not say
        // that, so an answer queued from an EARLIER request was applied to a later one -- a new
        // session registered under the relay's answer about an old code. Only _inbox.Clear() in
        // StopHosting prevented it: a guard in one method covering an unchecked assumption in
        // another. The refusal arm needs it more, not less: a stale refusal makes the host abandon a
        // code nobody refused. FALSE rather than a drop, so the frame falls through instead of being
        // CONSUMED by a branch that did nothing with it -- which is BUG-43 exactly.
        if (host.Code is not { } outstanding
            || !string.Equals(envelope.SessionCode, outstanding.Value, StringComparison.Ordinal))
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
    internal static void ApplyContent(
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
    internal static byte[]? Apply(
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
            });}
