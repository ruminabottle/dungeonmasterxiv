using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Connects a session's collaborators to the four inbound doors, once per frame.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SUPPLY OF THE HANDLERS IS NOT THE ADVANCING OF A FRAME (DMXENG-65).</b>
/// <see cref="SessionCoordinator.Tick"/> was fifty-one lines and roughly twenty-five of them were
/// this: deciding which collaborator answers each door, and explaining why. Those are two reasons to
/// change — a new inbound message type edits the wiring, a new per-frame obligation edits the tick —
/// and they were one method.
/// </para>
/// <para>
/// <b>The point is WHERE THE NEXT HANDLER LANDS, not the line count.</b> Every handler added since
/// DMXENG-50 has enlarged <see cref="SessionCoordinator"/>, which is how that class reached margin 3
/// and blocked the chunk behind it for the fifth time. A fifth door now edits <i>this</i> type and
/// leaves the coordinator's size unchanged.
/// </para>
/// <para>
/// <b>Built per frame rather than held as a field, and that is deliberate.</b> Holding it would cost
/// <see cref="SessionCoordinator"/> a field and five constructor lines — and that constructor is
/// already at margin 6 on the method row, so paying there to save here would move the pressure rather
/// than relieve it.
/// <para>
/// <b>A class rather than a struct, which was not the first choice.</b> A <c>readonly struct</c> is
/// the obvious shape for three references rebuilt each frame, and it does not compile: <b>CS9111</b>
/// forbids a lambda inside a struct instance member from capturing a primary constructor parameter,
/// and every door here is a lambda. Copying the parameters to fields does not help — capturing a
/// struct field captures <c>this</c> by reference, which is refused for the same reason.
/// </para>
/// <para>
/// <b>The allocation is one small object on a path that already allocates five delegates.</b> The
/// handler lambdas below were heap-allocated per frame before this type existed and still are, so
/// this is consistent with what <see cref="SessionCoordinator.Tick"/> already did rather than a new
/// cost introduced by moving it.
/// </para>
/// </para>
/// </remarks>
/// <param name="admissions">Where an admission-time signal goes.</param>
/// <param name="resources">Holds the per-peer keys and the member-content record.</param>
/// <param name="resolveRelink">
/// Turns the raw claimed-participant string into a <see cref="RelinkClaim"/>. Supplied rather than
/// looked up because deciding whether a claimed participant is one this campaign knows needs the
/// campaign, which is not Core's to reach.
/// </param>
internal sealed class InboundWiring(
    AdmissionControl admissions,
    SessionResources resources,
    Func<string?, RelinkClaim> resolveRelink)
{
    /// <summary>The handlers for one frame.</summary>
    /// <param name="now">The instant this frame is being advanced at.</param>
    /// <param name="sessionKey">The joiner's shared key, or null before one exists.</param>
    /// <param name="onHostContent">
    /// What to do with host-authored content. Passed in rather than built here because it writes
    /// the coordinator's own received roster, which is the one piece of this that is not a
    /// collaborator's business.
    /// </param>
    public InboundHandlers For(
        DateTimeOffset now,
        byte[]? sessionKey,
        Action<SessionContent> onHostContent) =>
        new(
            // T-37: the claim is RESOLVED HERE, at the one place that has both the wire and
            // the campaign. Until now it arrived on the envelope and was dropped -- the joiner
            // sent it, the relay routed it, and every relink branch took the not-a-relink path
            // because Receive was only ever reached with RelinkClaim.None.
            Admission: new JoinerAdmission(
                OnJoinRequest: (key, name, claimed) =>
                    admissions.AdmitToTheQueue(key, now, name, resolveRelink(claimed)),
                OnComparabilityReceipt: admissions.RecordComparabilityReceipt),
            HostAuthored: new HostAuthoredContent(
                OpenWith: sessionKey,
                OnContent: onHostContent),
            // R-1.3k. DELIBERATELY NOT onHostContent: that is what a JOINER was told, and letting a
            // member reach it would invert D-3 -- see MemberAuthoredContent.OnContent. Since
            // DMXENG-59 the two doors are two TYPES, so the swap will not compile either.
            MemberAuthored: new MemberAuthoredContent(
                OpenWith: resources.MemberKeys.Candidates,
                // A-1.16a. RECORDED FIRST, THEN ACTED ON: the receipt is what a DM's UI reads, and a
                // departure that removed the member without leaving a trace would take them off the
                // roster with nothing anywhere saying why.
                //
                // The peer code comes from the KEY THE PAYLOAD OPENED UNDER, never from the payload,
                // so a member can only ever remove itself -- see MemberContentReader.
                OnContent: (peer, content) =>
                {
                    resources.MemberContent.Record(peer, content);

                    if (content.Leaving is true)
                    {
                        // R-2.12 / SQ-116: THIS CLIENT WRITES DOWN WHAT IT RECEIVED, and a member
                        // saying it is leaving is one of the few things that actually arrives today.
                        // Recorded BEFORE the departure is acted on, for A-1.16a's reason one row up:
                        // acting first and recording second leaves a window where the member is gone
                        // and nothing says why.
                        resources.Recording.RecordAsHost(StreamEventKind.Left, peer, string.Empty, now);

                        admissions.Departed(peer);
                    }
                }),
            // A-1.28, and the RELAY authors this one -- see TransportNotices for why that is
            // permissible and where the line is. RecordDrop refuses a key this host has not
            // admitted, so a stranger's notice records nothing.
            Transport: new TransportNotices(
                OnConnectionDropped: key => admissions.RecordDrop(key, now)));
}
