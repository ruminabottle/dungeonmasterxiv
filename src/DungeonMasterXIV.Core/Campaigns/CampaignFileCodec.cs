using System.Text.Json;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Turns a single-campaign file into text and back.
/// </summary>
/// <remarks>
/// Deserialization is total: it reports failure rather than throwing, because the response to a
/// campaign file that will not read is to list it so the DM can delete it (A-1.10), not to fail
/// the plugin's load.
/// </remarks>
public static class CampaignFileCodec
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// Serializes one campaign, stamping the version this build writes.
    /// </summary>
    /// <param name="campaign">The campaign to store.</param>
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

    /// <summary>
    /// Reads a campaign file. Returns false for anything this build cannot faithfully read: text
    /// that will not parse, a file from a newer schema version, or one carrying no campaign.
    /// </summary>
    /// <param name="stored">The file's text.</param>
    /// <param name="campaign">The campaign, or <c>null</c> on failure.</param>
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
