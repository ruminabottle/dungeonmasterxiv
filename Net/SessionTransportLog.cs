using System;
using Dalamud.Plugin.Services;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Transport;

/// <summary>
/// Forwards the session transport's log lines to Dalamud.
/// </summary>
/// <remarks>
/// The plugin-side half of the seam, matching <c>CampaignStoreLog</c>: Core cannot name
/// <see cref="IPluginLog"/>, so this adapter does the naming and Core keeps the decisions. It holds
/// no logic of its own on purpose — anything it decided would be a decision that could not be tested.
/// </remarks>
public sealed class SessionTransportLog : ISessionTransportLog
{
    private readonly IPluginLog _log;

    /// <param name="log">Dalamud's log.</param>
    public SessionTransportLog(IPluginLog log) => _log = log;

    /// <inheritdoc />
    public void Information(string message) => _log.Information(message);

    /// <inheritdoc />
    public void Warning(string message) => _log.Warning(message);

    /// <inheritdoc />
    public void Warning(Exception exception, string message) => _log.Warning(exception, message);
}
