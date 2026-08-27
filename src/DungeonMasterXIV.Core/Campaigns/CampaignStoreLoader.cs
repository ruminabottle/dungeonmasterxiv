using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// The load path: migrate the old single-file store if it is still there, then read every campaign
/// file and classify what would not read.
/// </summary>
/// <remarks>
/// Migration lives here because the standards put it on the load path — the only point that knows
/// what shape arrived. Nothing else in the store may write the old file, and nothing outside this
/// type needs to know it ever existed.
/// </remarks>
public static class CampaignStoreLoader
{
    /// <summary>Loads everything the archive holds.</summary>
    /// <param name="archive">Where the files are.</param>
    /// <param name="log">Where outcomes are reported. Never receives a participant label.</param>
    public static CampaignLoadResult Load(ICampaignArchive archive, ICampaignStoreLog log)
    {
        var result = new CampaignLoadResult();

        Migrate(archive, log, result);
        ReadCampaignFiles(archive, result);
        CollectFilesLeftBehind(archive, result);
        Report(log, result);

        return result;
    }

    /// <summary>
    /// Moves a v1 single-file store onto the per-campaign layout. The old file is deleted only
    /// after every campaign in it has been written, so an interrupted migration leaves it intact
    /// and is retried on the next load rather than losing half the campaigns.
    /// </summary>
    private static void Migrate(ICampaignArchive archive, ICampaignStoreLog log, CampaignLoadResult result)
    {
        var legacy = archive.ReadLegacy();
        if (legacy is null)
        {
            return;
        }

        if (!CampaignDocumentCodec.TryDeserialize(legacy, out var document) || document is null)
        {
            // Left exactly as it is. It is picked up as a file left behind, so the DM can see and
            // delete it — overwriting or discarding it here is the data loss the persisted-data
            // rule forbids for this store.
            log.Warning(
                $"The previous campaign store '{CampaignFileName.LegacyFileName}' could not be read, " +
                "so it has been left untouched and is listed for you to remove.");
            return;
        }

        foreach (var campaign in document.Campaigns)
        {
            archive.WriteCampaign(CampaignFileName.NameFor(campaign.CampaignId), CampaignFileCodec.Serialize(campaign));
        }

        archive.Delete(CampaignFileName.LegacyFileName);
        result.Migrated = document.Campaigns.Count;
    }

    private static void ReadCampaignFiles(ICampaignArchive archive, CampaignLoadResult result)
    {
        foreach (var name in archive.CampaignFiles())
        {
            var stored = archive.ReadCampaign(name);

            if (stored is not null && CampaignFileCodec.TryDeserialize(stored, out var campaign) && campaign is not null)
            {
                result.Campaigns.Add(campaign);
            }
            else
            {
                result.Unreadable.Add(new UnreadableCampaignFile(name, CampaignFileProblem.WillNotParse));
            }
        }
    }

    private static void CollectFilesLeftBehind(ICampaignArchive archive, CampaignLoadResult result)
    {
        foreach (var name in archive.OtherOwnedFiles())
        {
            result.Unreadable.Add(new UnreadableCampaignFile(name, CampaignFileProblem.LeftByAnEarlierBuild));
        }
    }

    private static void Report(ICampaignStoreLog log, CampaignLoadResult result)
    {
        if (result.Migrated > 0)
        {
            log.Information(
                $"Moved {result.Migrated} campaign(s) out of the previous single-file store into one file each.");
        }

        if (result.Campaigns.Count == 0 && result.Unreadable.Count == 0)
        {
            result.Outcome = CampaignLoadOutcome.FirstRun;
            log.Information("No campaigns found. This machine has not saved a campaign before.");
            return;
        }

        result.Outcome = result.Campaigns.Count > 0
            ? CampaignLoadOutcome.Loaded
            : CampaignLoadOutcome.Unreadable;

        log.Information(
            $"Loaded {result.Campaigns.Count} campaign(s); {result.Unreadable.Count} file(s) could not be read.");
    }
}
