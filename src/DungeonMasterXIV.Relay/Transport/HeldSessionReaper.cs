namespace DungeonMasterXIV.Relay.Transport;

/// <summary>Every few seconds, ends the sessions whose host has been away longer than the hold.</summary>
public sealed class HeldSessionReaper(RelayHub hub, RelayOptions options, ILogger<HeldSessionReaper> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await hub.ExpireHoldsAsync(options.HostHold, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "held-session expiry failed; will retry");
            }
        }
    }
}
