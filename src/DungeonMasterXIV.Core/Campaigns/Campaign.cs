using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Campaigns;

public sealed class Campaign
{
    public Guid CampaignId { get; set; }

    public string? PreferredCode { get; set; }

    public string? Name { get; set; }

    public string? DisplayNameAlias { get; set; }

    public List<CampaignParticipant> Participants { get; set; } = new();

    public DateTimeOffset CreatedUtc { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownProperties { get; set; }

    [JsonIgnore]
    public Dictionary<string, JsonElement>? FileUnknownProperties { get; set; }
}
