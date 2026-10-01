using System;

namespace DungeonMasterXIV.Net;

/// <summary>Issues stream stamps with increasing sequence numbers and the time its clock function supplies.</summary>
public sealed class HostSequencer
{
    private readonly Func<DateTimeOffset> _now;
    private long _next;

    public HostSequencer(Func<DateTimeOffset> now) => _now = now;

    public StreamStamp Next() => new(++_next, _now().UtcTicks);
}
