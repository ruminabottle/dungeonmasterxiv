using System;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A log that records nothing, for the tests that construct a <see cref="SessionCoordinator"/> and
/// are not about logging.
/// </summary>
internal sealed class SilentLog : ISessionTransportLog
{
    /// <summary>The shared instance. Stateless, so one is enough.</summary>
    public static readonly SilentLog Instance = new();

    /// <inheritdoc />
    public void Information(string message)
    {
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
    }

    /// <inheritdoc />
    public void Warning(Exception exception, string message)
    {
    }
}
