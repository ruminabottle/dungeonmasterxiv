using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What a client does to its session because the plugin is unloading (R-1.1, R-1.3g).
/// </summary>
/// <remarks>
/// <para>
/// <b>BUG-154: THE TEARDOWN RAN THE HOST'S HALF ONLY.</b> It called <c>StopHosting</c>,
/// <c>Detach</c> and disposed the transport — the first a no-op for a joiner, the other two just
/// dropping the socket, which is what an UNGRACEFUL DROP looks like from the relay. So a player who
/// quit FFXIV deliberately was indistinguishable from one whose machine died, and the host held
/// their seat five minutes under R-1.5a. The player said nothing because the code never said
/// anything.
/// </para>
/// <para>
/// <b>ITS OWN FILE BECAUSE <see cref="SessionCoordinator"/> HAS NO ROOM — measured, not assumed.</b>
/// That class is 399 lines against an absolute cap of 400, so it cannot take a five-line method, let
/// alone a documented one. This is not a workaround for the limit: the ordering below is a teardown
/// POLICY composed from operations the coordinator already exposes publicly, and it holds no state
/// of its own, so an extension is where it belongs rather than where it was pushed.
/// </para>
/// </remarks>
public static class SessionTeardown
{
    /// <summary>
    /// Ends <paramref name="coordinator"/>'s session in the order that lets a joiner be heard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE ANNOUNCEMENT GOES FIRST, AND THE ORDER IS THE FIX.</b> Once the transport is gone
    /// there is nothing to send on, so a departure placed after it compiles, returns false, sends
    /// nothing, and reads correctly in a diff. Measured while building this: with the announcement
    /// moved AFTER the detach, the test asserting a departure is sent STILL PASSED, and only the
    /// test asserting the ORDER caught it.
    /// </para>
    /// <para>
    /// <b>What that same measurement showed, stated rather than overclaimed:</b> the detach
    /// unsubscribes from receiving and does not by itself stop a send. The ordering that decides the
    /// outcome in the product is announcing before the transport is DISPOSED, which the caller owns —
    /// and with this method there is exactly one line left after it at that call site. Leaving before
    /// detaching is kept because it costs nothing and because a detach's meaning could widen.
    /// </para>
    /// <para>
    /// <b>A HOST ANNOUNCES NOTHING, and is not special-cased to achieve it:</b> a departure needs a
    /// session code AND a shared key from having been admitted, so a host and a never-admitted joiner
    /// both fall out of it silently. Pinned by test rather than inferred from that reasoning, because
    /// the bug report was right that it was only ever a reading of a comment.
    /// </para>
    /// <para>
    /// <b>This does NOT make a host remove members who vanished.</b> R-1.3g names that as the false
    /// repair that breaks R-1.5a: the seat hold on a drop is correct and is untouched here. The only
    /// thing that changed is that a DELIBERATE quit now says so.
    /// </para>
    /// </remarks>
    /// <param name="coordinator">The client being torn down.</param>
    /// <param name="now">When the session ended.</param>
    public static void EndSessionForTeardown(this SessionCoordinator coordinator, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        coordinator.Membership.Leave();
        coordinator.StopHosting(now);
        coordinator.Detach();
    }
}
