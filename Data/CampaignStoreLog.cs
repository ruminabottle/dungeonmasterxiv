using Dalamud.Plugin.Services;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Data;

/// <summary>Passes the campaign store's log messages to the Dalamud plugin log.</summary>
public sealed class CampaignStoreLog : ICampaignStoreLog
{
    private readonly IPluginLog _log;

    public CampaignStoreLog(IPluginLog log) => _log = log;

    public void Information(string message) => _log.Information(message);

    public void Warning(string message) => _log.Warning(message);
}
