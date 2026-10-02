using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A session log that records nothing.</summary>
internal sealed class QuietLog : ISessionTransportLog
{
    public static readonly QuietLog Instance = new();

    public void Information(string message)
    {
    }

    public void Warning(string message)
    {
    }

    public void Warning(Exception exception, string message)
    {
    }
}
