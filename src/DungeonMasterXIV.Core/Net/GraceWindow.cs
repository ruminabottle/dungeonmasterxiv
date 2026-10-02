using System;

namespace DungeonMasterXIV.Net;

/// <summary>Counts down a set time once a connection is lost and reports when it runs out, unless it returns.</summary>
public sealed class GraceWindow
{
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(5);

    private readonly TimeSpan _length;
    private TimeSpan _elapsed;

    public GraceWindow(TimeSpan? length = null)
    {
        _length = length ?? Default;

        if (!TransportContract.IsKeepAliveSafeFor(_length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                _length,
                "A grace window shorter than three keepalive intervals ends live sessions during an ordinary lull.");
        }
    }

    public bool IsRunning { get; private set; }

    public bool HasExpired { get; private set; }

    public TimeSpan Remaining => _length - _elapsed < TimeSpan.Zero ? TimeSpan.Zero : _length - _elapsed;

    public TimeSpan Elapsed => _elapsed;

    public void HostLost()
    {
        if (IsRunning || HasExpired)
        {
            return;
        }

        IsRunning = true;
        _elapsed = TimeSpan.Zero;
    }

    public bool HostReturned()
    {
        if (!IsRunning)
        {
            return false;
        }

        IsRunning = false;
        _elapsed = TimeSpan.Zero;
        return true;
    }

    public bool Tick(TimeSpan sinceLastTick)
    {
        if (!IsRunning)
        {
            return false;
        }

        _elapsed += sinceLastTick;
        if (Remaining > TimeSpan.Zero)
        {
            return false;
        }

        IsRunning = false;
        HasExpired = true;
        return true;
    }

    public void Reset()
    {
        IsRunning = false;
        HasExpired = false;
        _elapsed = TimeSpan.Zero;
    }
}
