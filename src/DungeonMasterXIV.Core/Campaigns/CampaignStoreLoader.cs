using System;
using System.Collections.Generic;
using System.IO;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Loads campaigns from an archive, first moving any single-file store into one file per campaign.</summary>
public static class CampaignStoreLoader
{
    public static CampaignLoadResult Load(ICampaignArchive archive, ICampaignStoreLog log)
    {
        var result = new CampaignLoadResult();

        Migrate(archive, log, result);
        ReadCampaignFiles(archive, result);
        CollectFilesLeftBehind(archive, result);
        Report(log, result);

        return result;
    }

    private static void Migrate(ICampaignArchive archive, ICampaignStoreLog log, CampaignLoadResult result)
    {
        var legacy = archive.ReadLegacy();
        if (legacy is null)
        {
            return;
        }

        if (!CampaignDocumentCodec.TryDeserialize(legacy, out var document) || document is null)
        {
            log.Warning(
                $"The previous campaign store '{CampaignFileName.LegacyFileName}' could not be read, " +
                "so it has been left untouched and is listed for you to remove.");
            return;
        }

        var written = new HashSet<string>(StringComparer.Ordinal);

        foreach (var campaign in document.Campaigns)
        {
            var name = CampaignFileName.NameFor(campaign.CampaignId);

            if (!written.Add(name))
            {
                log.Warning(
                    "Two stored campaigns share an identifier, so one of them could not be moved to " +
                    $"its own file. The previous store '{CampaignFileName.LegacyFileName}' has been " +
                    "kept because it is the only remaining copy.");
                continue;
            }

            if (!TryWrite(archive, log, name, campaign))
            {
                result.MigrationIncomplete = true;
                result.Migrated = written.Count - 1;
                return;
            }
        }

        result.Migrated = written.Count;

        if (written.Count == document.Campaigns.Count)
        {
            archive.Delete(CampaignFileName.LegacyFileName);
            return;
        }

        result.MigrationIncomplete = true;
    }

    private static bool TryWrite(ICampaignArchive archive, ICampaignStoreLog log, string name, Campaign campaign)
    {
        try
        {
            archive.WriteCampaign(name, CampaignFileCodec.Serialize(campaign));
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            log.Warning(
                $"Could not write '{name}' while moving campaigns out of the previous store: " +
                $"{failure.Message}. The previous store has been kept and this will be retried on " +
                "the next load. No campaign has been lost.");
            return false;
        }
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
            var problem = result.MigrationIncomplete
                && string.Equals(name, CampaignFileName.LegacyFileName, StringComparison.Ordinal)
                    ? CampaignFileProblem.StillHoldsCampaigns
                    : CampaignFileProblem.LeftByAnEarlierBuild;

            result.Unreadable.Add(new UnreadableCampaignFile(name, problem));
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
