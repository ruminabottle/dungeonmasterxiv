using Dalamud.Plugin.Services;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Forwards the campaign store's log lines to Dalamud's plugin log. A logic-free adapter, so that
/// <c>IPluginLog</c> — a Dalamud type — never has to be named from the project the tests can see.
/// </summary>
public sealed class CampaignStoreLog : ICampaignStoreLog
{
    private readonly IPluginLog _log;

    /// <param name="log">Dalamud's log for this plugin.</param>
    public CampaignStoreLog(IPluginLog log) => _log = log;

    /// <inheritdoc />
    public void Information(string message) => _log.Information(message);

    /// <inheritdoc />
    public void Warning(string message) => _log.Warning(message);
}
