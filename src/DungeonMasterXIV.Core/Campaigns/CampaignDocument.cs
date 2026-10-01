using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Campaigns;

/// <summary>The JSON layout of the single-file campaign store: a schema version and a list of campaigns.</summary>
public sealed class CampaignDocument
{
    public const int CurrentSchemaVersion = 1;

    public int Version { get; set; } = CurrentSchemaVersion;

    public List<Campaign> Campaigns { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownProperties { get; set; }
}
