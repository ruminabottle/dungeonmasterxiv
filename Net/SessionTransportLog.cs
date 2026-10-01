using System;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Transport;

/// <summary>Passes the session transport's log messages to the Dalamud plugin log.</summary>
public sealed class SessionTransportLog : ISessionTransportLog
{
    private readonly IPluginLog _log;

    public SessionTransportLog(IPluginLog log) => _log = log;

    public void Information(string message) => _log.Information(message);

    public void Warning(string message) => _log.Warning(message);

    public void Warning(Exception exception, string message) => _log.Warning(exception, message);
}
