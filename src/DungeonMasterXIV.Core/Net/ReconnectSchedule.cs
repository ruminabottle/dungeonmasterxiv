using System;

namespace DungeonMasterXIV.Net;

/// <summary>Spaces out redial attempts after a drop: about 1, 2, 4 and 8 seconds, then every 15.</summary>
internal sealed class ReconnectSchedule
{
    private static readonly TimeSpan[] Waits =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(15),
    ];

    private int _attempt;
    private TimeSpan _waited;

    public bool Due(TimeSpan sinceLastTick)
    {
        _waited += sinceLastTick;
        if (_waited < Waits[Math.Min(_attempt, Waits.Length - 1)])
        {
            return false;
        }

        _waited = TimeSpan.Zero;
        _attempt++;
        return true;
    }

    public void Reset()
    {
        _attempt = 0;
        _waited = TimeSpan.Zero;
    }
}
