using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Campaigns;

/// <summary>The JSON layout of one campaign's file: a schema version, the campaign and any unknown properties.</summary>
public sealed class CampaignFileDocument
{
    public const int CurrentSchemaVersion = 1;

    public int Version { get; set; } = CurrentSchemaVersion;

    public Campaign? Campaign { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownProperties { get; set; }
}
