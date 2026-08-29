using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The one place a <see cref="StreamStamp"/> is minted: the host's counter and the host's clock
/// (R-2.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE HOST SEQUENCES AND TIMESTAMPS EVERYTHING, so exactly one object does both.</b> Splitting
/// the counter from the clock would let a caller advance one without the other, and a stream entry
/// with a fresh ordinal and a stale time is a disagreement the log cannot show.
/// </para>
/// <para>
/// <b>The clock is injected and this type is the only holder of one on this path.</b> That is what
/// makes A-2.5 checkable rather than hoped for: a member's build constructs no sequencer, so there is
/// no clock anywhere in the receiving path to leak into the log. <b>The absence is the mechanism</b> —
/// see <see cref="SessionStream"/>, which takes stamps and has no way to make one.
/// </para>
/// <para>
/// <b>Host sequencing is not the relay deciding, and D-3 is untouched.</b> This runs on the host's
/// own client, which the session already treats as authoritative for shared state. The relay stays a
/// dumb pipe that stores nothing (D-2).
/// </para>
/// </remarks>
public sealed class HostSequencer
{
    private readonly Func<DateTimeOffset> _now;
    private long _next;

    /// <param name="now">
    /// The host's clock, read at the moment of sequencing rather than captured, because a session
    /// outlives any single reading of it.
    /// </param>
    public HostSequencer(Func<DateTimeOffset> now) => _now = now;

    /// <summary>
    /// Takes the next ordinal and stamps it with the host's clock, as one indivisible step.
    /// </summary>
    /// <remarks>
    /// <b>Sequence starts at 1, not 0.</b> Zero is what an absent or defaulted stamp reads as, and a
    /// log whose first real entry is indistinguishable from an uninitialised one cannot be checked.
    /// </remarks>
    public StreamStamp Next() => new(++_next, _now().UtcTicks);
}
