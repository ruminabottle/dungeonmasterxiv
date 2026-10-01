using System.Text.Json;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Converts one campaign to and from its own JSON file, refusing a newer schema version.</summary>
public static class CampaignFileCodec
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(Campaign campaign)
    {
        var document = new CampaignFileDocument
        {
            Version = CampaignFileDocument.CurrentSchemaVersion,
            Campaign = campaign,
            UnknownProperties = campaign.FileUnknownProperties,
        };

        return JsonSerializer.Serialize(document, Options);
    }

    public static bool TryDeserialize(string stored, out Campaign? campaign)
    {
        campaign = null;

        CampaignFileDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<CampaignFileDocument>(stored);
        }
        catch (JsonException)
        {
            return false;
        }

        if (document?.Campaign is null || document.Version > CampaignFileDocument.CurrentSchemaVersion)
        {
            return false;
        }

        campaign = document.Campaign;
        campaign.FileUnknownProperties = document.UnknownProperties;
        return true;
    }
}
