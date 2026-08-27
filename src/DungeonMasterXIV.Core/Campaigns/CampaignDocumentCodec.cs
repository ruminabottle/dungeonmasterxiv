using System;
using System.Text.Json;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Turns a <see cref="CampaignDocument"/> into the text that is stored, and back.
/// </summary>
/// <remarks>
/// Deserialization is deliberately total: it reports failure rather than throwing, because the
/// caller's response to an unreadable document is to preserve it and carry on, not to fail the
/// plugin's load.
/// </remarks>
public static class CampaignDocumentCodec
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// Serializes <paramref name="document"/>, stamping it with the version this build writes.
    /// The stamp happens here, immediately before the text exists, so a document can never be
    /// written carrying the version it was loaded under.
    /// </summary>
    /// <param name="document">The document to write.</param>
    public static string Serialize(CampaignDocument document)
    {
        document.Version = CampaignDocument.CurrentSchemaVersion;
        return JsonSerializer.Serialize(document, Options);
    }

    /// <summary>
    /// Reads a stored document. Returns false for anything this build cannot faithfully read:
    /// malformed text, and — just as importantly — a document written by a newer build, which is
    /// well-formed and still not ours to interpret.
    /// </summary>
    /// <param name="stored">The text that was stored.</param>
    /// <param name="document">The parsed document, or <c>null</c> on failure.</param>
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
