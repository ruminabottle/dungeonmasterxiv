using System;
using System.Collections.Generic;
using System.IO;

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
    /// Moves a v1 single-file store onto the per-campaign layout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The old file is deleted only when every campaign in it reached a file of its own.</b> Not
    /// "when the loop finished" — the loop finishing says nothing about what landed. A v1 store can
    /// contain two campaigns sharing a <c>CampaignId</c>, which resolve to one filename, so the
    /// second overwrites the first and one campaign is destroyed rather than merely unlisted. That
    /// state is unreachable from <c>Create</c>, which uses a fresh UUID, so it arrives only from a
    /// hand-edited file, a restored backup, or two machines' folders merged — and a fixture built
    /// from the previous writer cannot produce it.
    /// </para>
    /// <para>
    /// <b>The count is derived from what was written, never from what was read.</b> Counting the
    /// input campaigns produces a number that cannot report a failure to write: it would say "moved
    /// 2" while one file exists.
    /// </para>
    /// </remarks>
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

        var written = new HashSet<string>(StringComparer.Ordinal);

        foreach (var campaign in document.Campaigns)
        {
            var name = CampaignFileName.NameFor(campaign.CampaignId);

            if (!written.Add(name))
            {
                // A second campaign with an id already used. Writing it would destroy the first, so
                // it is skipped and the old file is kept below — that file is now the only copy of
                // this campaign, and losing it silently is the outcome this whole branch prevents.
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

    /// <summary>
    /// Writes one campaign, reporting rather than throwing when the disk refuses.
    /// </summary>
    /// <remarks>
    /// This path runs once for every existing user, on upgrade, inside the store's constructor. An
    /// exception here would stop the plugin loading at all over a transient write failure. The
    /// failure is logged with context and the old file is kept, so the migration is retried on the
    /// next load rather than silently abandoned — that is reporting, not swallowing.
    /// </remarks>
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
            // The legacy file gets its own wording when it was kept because campaigns could not be
            // moved out of it. Describing it as "not used any more" would be false, and it is the
            // one file where that sentence could cost a DM their campaigns.
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
