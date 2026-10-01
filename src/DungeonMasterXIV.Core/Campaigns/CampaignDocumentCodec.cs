using System;
using System.Text.Json;

namespace DungeonMasterXIV.Campaigns;

public static class CampaignDocumentCodec
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(CampaignDocument document)
    {
        document.Version = CampaignDocument.CurrentSchemaVersion;
        return JsonSerializer.Serialize(document, Options);
    }

    public static bool TryDeserialize(string stored, out CampaignDocument? document)
    {
        document = null;

        try
        {
            document = JsonSerializer.Deserialize<CampaignDocument>(stored);
        }
        catch (JsonException)
        {
            return false;
        }

        if (document is null || document.Version > CampaignDocument.CurrentSchemaVersion)
        {
            document = null;
            return false;
        }

        return true;
    }
}
