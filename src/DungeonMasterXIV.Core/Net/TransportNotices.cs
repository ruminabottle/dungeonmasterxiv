using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What the RELAY tells this client about the transport, as distinct from what a peer tells it
/// about the session.
/// </summary>
/// <remarks>
/// <para>
/// <b>A FOURTH DOOR RATHER THAN A MEMBER OF AN EXISTING ONE, and the boundary is D-2's.</b> The
/// other three groups are all peer-authored: a joiner asking to be admitted, a host publishing
/// state, a member publishing its own. <b>This one is authored by the relay</b>, which D-2 says is
/// not authoritative over the session — so it belongs behind its own door, where a reader can see
/// at a glance which side of that line a handler sits on.
/// </para>
/// <para>
/// <b>What makes a relay-authored notice permissible at all is that it is about the TRANSPORT.</b>
/// A dropped connection is something only the relay can observe, and R-1.7b already has it
/// authoring a transport message while R-1.9 already discloses that it sees connections exist. <b>A
/// relay-authored roster change would be a different thing entirely and is forbidden</b> — which is
/// why this door has one member and why the member is named for what was seen rather than for what
/// should follow.
/// </para>
/// <para>
/// <b>Four groups is compliant and silent</b> — the parameter flag is above four, not at it, as the
/// tool's own probe shows at 399/400/401 on the rows that print a margin. Stated because two people
/// read that boundary two ways in one night, and because a fifth group is the point at which this
/// record should be argued about rather than added to.
/// </para>
/// </remarks>
/// <param name="OnConnectionDropped">
/// A member's connection went away (A-1.28). Carries the key that member joined with, because that
/// is the only name the relay and the host share — the host derives its own peer code from it.
/// <para>
/// <b>Nothing here removes anybody.</b> R-1.5a holds the seat and A-1.29 forbids a roster change on
/// the relay's say-so; what this records is <i>when</i>, for the decision the host makes if that
/// member comes back.
/// </para>
/// </param>
public readonly record struct TransportNotices(
    Action<byte[]>? OnConnectionDropped = null)
{
    /// <summary>
    /// Hands <paramref name="envelope"/> to whichever handler this door holds for it (A-1.28).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Here rather than in <c>AdmissionInbox.Drain</c>, and the reason is a measurement.</b>
    /// <c>Drain</c> is 173 lines against a 60 capacity — BUG-103's largest entry — and the other
    /// 173 are not this chunk's to repair. But where its OWN lines go is its to choose, so the arm
    /// there is three lines and the reasoning is here. <b>Declining to enlarge a breach is not the
    /// same as fixing one.</b>
    /// </para>
    /// <para>
    /// <b>NO GUARD ON WHO SENT IT, and that is not an oversight.</b> Anyone can put bytes on this
    /// channel, so a notice naming a stranger must be HARMLESS rather than refused here — and it
    /// is: <c>AdmissionControl.RecordDrop</c> resolves the key to a peer code and records nothing
    /// unless that member is admitted to THIS session. <b>The check belongs where the roster is,
    /// not where the bytes arrive</b> — the same placement BUG-57 settled for peer-code vetting.
    /// </para>
    /// <para>
    /// <b>A notice with no key is dropped silently</b>, like any frame on this path that does not
    /// parse. Existing rule rather than a new answer to what the DM should be told about it.
    /// </para>
    /// </remarks>
    /// <param name="envelope">The notice as it arrived.</param>
    public void Deliver(WireEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (OnConnectionDropped is { } onDropped && envelope.PublicKey is { } memberKey)
        {
            onDropped(memberKey);
        }
    }
}
