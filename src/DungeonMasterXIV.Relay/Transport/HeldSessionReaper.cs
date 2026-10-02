namespace DungeonMasterXIV.Relay.Transport;

/// <summary>Every few seconds, ends the sessions whose host has been away longer than the hold.</summary>
public sealed class HeldSessionReaper(RelayHub hub, RelayOptions options) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await hub.ExpireHoldsAsync(options.HostHold, stoppingToken).ConfigureAwait(false);
        }
    }
}
