using System;

namespace DungeonMasterXIV.Net;

/// <summary>The relay connection's keepalive timings, and a check that a grace window outlasts three keepalives.</summary>
public static class TransportContract
{
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(90);

    public const int RequiredGraceMargin = 3;

    public static bool IsKeepAliveSafeFor(TimeSpan graceWindow) =>
        graceWindow >= KeepAliveInterval * RequiredGraceMargin;
}
