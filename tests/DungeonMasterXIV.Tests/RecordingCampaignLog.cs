using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Captures the store's log lines so a test can assert both what was said and — for D-8 — what
/// was not.
/// </summary>
internal sealed class RecordingCampaignLog : ICampaignStoreLog
{
    public List<string> Informations { get; } = new();

    public List<string> Warnings { get; } = new();

    /// <summary>Every line written, at any level.</summary>
    public IEnumerable<string> AllLines => Informations.Concat(Warnings);

    public void Information(string message) => Informations.Add(message);

    public void Warning(string message) => Warnings.Add(message);
}
