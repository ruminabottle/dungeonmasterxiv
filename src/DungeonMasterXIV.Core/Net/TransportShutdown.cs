using System;
using System.Threading;
using System.Threading.Tasks;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Runs the close-then-dispose sequence a transport must follow when it lets a connection go.
/// </summary>
/// <remarks>
/// <para>
/// Disposing a socket without closing it first never puts a close frame on the wire, so the relay is
/// not told and keeps the connection object, its buffers and its session association until its own
/// idle reaper fires. That makes the relay's reaper absorb every ordinary disconnect as though it
/// were a client that vanished silently, which is not what it is sized for.
/// </para>
/// <para>
/// The bound is the other half and is not optional. An unbounded close is a plugin that will not
/// unload when the peer is dead or hostile, which is the A-0.6 failure arriving through a new door.
/// Trading a half-open socket for a hung shutdown would be a worse bug than the one being fixed.
/// </para>
/// <para>
/// Expressed over delegates rather than over a socket type deliberately: the standards keep sockets
/// in the plugin's <c>Net/</c> and nowhere else, so the ordering can be tested here without
/// <c>System.Net.WebSockets</c> entering Core and without a Dalamud-bound type entering the test
/// assembly.
/// </para>
/// </remarks>
public static class TransportShutdown
{
    /// <summary>
    /// How long a close handshake may take before shutdown stops waiting and disposes anyway.
    /// </summary>
    /// <remarks>
    /// Sized for sending a close frame on a live connection, not for a round trip: the caller uses
    /// the output-only close, so this waits for the frame to leave rather than for the peer to
    /// answer. It is deliberately not in <see cref="TransportContract"/> — that type is the set of
    /// values the plugin and the relay must agree on, and how long this end is willing to wait
    /// before giving up on its own shutdown is not one of them.
    /// </remarks>
    public static readonly TimeSpan CloseTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Attempts <paramref name="closeAsync"/>, then runs <paramref name="dispose"/> whether or not
    /// the close succeeded, timed out, or threw.
    /// </summary>
    /// <param name="closeAsync">
    /// Starts the close handshake. Receives a token cancelled once <paramref name="bound"/> elapses,
    /// so a close that honours it stops rather than running on after nobody is waiting.
    /// </param>
    /// <param name="dispose">Releases the connection. Always runs, exactly once.</param>
    /// <param name="bound">How long to wait for the close before disposing regardless.</param>
    /// <returns>
    /// <c>null</c> when the close completed within the bound; otherwise why it did not — a
    /// <see cref="TimeoutException"/> if it simply did not finish, or the exception it failed with.
    /// The caller owns the log, so this reports the reason rather than swallowing it.
    /// </returns>
    public static Exception? CloseThenDispose(
        Func<CancellationToken, Task> closeAsync,
        Action dispose,
        TimeSpan bound)
    {
        ArgumentNullException.ThrowIfNull(closeAsync);
        ArgumentNullException.ThrowIfNull(dispose);

        try
        {
            using var bounded = new CancellationTokenSource(bound);

            return closeAsync(bounded.Token).Wait(bound)
                ? null
                : new TimeoutException($"The close handshake did not complete within {bound}.");
        }
        catch (Exception exception)
        {
            // Wait surfaces a faulted task as an AggregateException; the inner one is the useful
            // half. A close that throws is still a close that was attempted, and disposal below is
            // what stops the failure turning into a leaked connection on this end as well.
            return exception is AggregateException { InnerException: { } inner } ? inner : exception;
        }
        finally
        {
            dispose();
        }
    }
}
