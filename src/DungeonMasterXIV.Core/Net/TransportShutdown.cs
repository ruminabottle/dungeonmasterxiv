using System;
using System.Threading;
using System.Threading.Tasks;

namespace DungeonMasterXIV.Net;

/// <summary>Closes a connection within a time limit, always disposes it, and returns any failure.</summary>
public static class TransportShutdown
{
    public static readonly TimeSpan CloseTimeout = TimeSpan.FromMilliseconds(250);

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
            return exception is AggregateException { InnerException: { } inner } ? inner : exception;
        }
        finally
        {
            dispose();
        }
    }
}
