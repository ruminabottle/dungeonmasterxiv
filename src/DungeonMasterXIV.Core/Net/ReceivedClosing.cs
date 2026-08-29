namespace DungeonMasterXIV.Net;

/// <summary>
/// When the host has told this client the session ends, or nothing (R-1.3g).
/// </summary>
/// <remarks>
/// <para>
/// <b>The receiving half of a notice that has been sent since DMXENG-58 and read by nobody.</b>
/// <c>RosterBroadcast.PublishClosing</c> seals a closing instant to every participant; measured
/// before this file, <see cref="SessionClosing"/> had ZERO occurrences under <c>Windows/</c> or
/// <c>Plugin.cs</c>. A participant of a session the DM had ended saw a roster that never changed and
/// was told nothing — the indefinite wait R-1.3c and R-1.8 both forbid, arriving through the one
/// path that was supposed to prevent it.
/// </para>
/// <para>
/// <b>The inverse of <see cref="ReceivedRoster"/>, and empty on the host for the same reason.</b>
/// D-3 makes the host the author of both; a host reading its own broadcast back would be believing
/// a copy of what it already decided.
/// </para>
/// <para>
/// <b>It holds an instant and computes nothing.</b> R-1.3g's sixty seconds are applied once, by the
/// host, in <see cref="SessionClosing.DecidedByHost"/>; the countdown a participant watches is
/// <see cref="SessionClosing.RemainingAt"/> reading that instant. A second place that knew the
/// duration is how a host and a client come to disagree, which is the drift R-1.3c names in terms.
/// </para>
/// </remarks>
internal sealed class ReceivedClosing
{
    /// <summary>What the host said, or null if it has said nothing.</summary>
    public SessionClosing? Notice { get; private set; }

    /// <summary>
    /// Takes what arrived in a payload, if it carried a closing instant at all.
    /// </summary>
    /// <param name="utcTicks">The instant from the message, or null.</param>
    /// <remarks>
    /// <para>
    /// <b>Null leaves any previous notice standing.</b> The closing instant is one optional field on
    /// a <see cref="SessionContent"/> and MOST PAYLOADS CARRY NONE — every ordinary roster push is a
    /// payload with no closing. A build that cleared on null would forget the notice on the very
    /// next message and put the participant back in the silence this type exists to end.
    /// </para>
    /// <para>
    /// <b>An out-of-range value also leaves it standing, rather than clearing it.</b> The value
    /// comes from another client and <see cref="SessionClosing.TryFromWire"/> is the only door;
    /// refusing it is right, and treating the refusal as "the session is no longer closing" would
    /// let a malformed number retract a notice the host actually sent.
    /// </para>
    /// </remarks>
    public void Apply(long? utcTicks)
    {
        if (utcTicks is { } ticks && SessionClosing.TryFromWire(ticks) is { } closing)
        {
            Notice = closing;
        }
    }

    /// <summary>
    /// Forgets the notice, because this client is no longer in the session it applied to.
    /// </summary>
    /// <remarks>
    /// A stale notice outliving its session would show a countdown for a session the user has left,
    /// or worse, close the next one they join. Nothing here decides WHEN that happens — leaving is
    /// the caller's act and this only stops remembering.
    /// </remarks>
    public void Clear() => Notice = null;
}
