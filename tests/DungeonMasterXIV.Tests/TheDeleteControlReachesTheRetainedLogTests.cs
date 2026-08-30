using System;
using System.Collections.Generic;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.12 and R-1.7a: deleting a campaign through the EXISTING control also deletes its retained
/// log, so the shipped sentence <i>"nothing to delete anywhere but here"</i> stays true now that
/// retention has put a second thing on disk.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CONTROL IS NOT NEW AND THAT IS THE POINT.</b> DMXENG-103's proof obligation is that a
/// retained log is deleted <i>by the existing control</i> — a second delete button would satisfy a
/// test asserting "the log can be deleted" while leaving the shipped copy false, because the copy
/// promises there is nothing to delete ANYWHERE ELSE.
/// </para>
/// <para>
/// <b>The negative is required rather than decorative.</b> The ticket asks for a fixture where a log
/// exists and is NOT deleted by an unrelated path — without it, every assertion here passes against
/// a build that deletes every log on any deletion, which would be a data-loss defect wearing a
/// green test.
/// </para>
/// </remarks>
public class TheDeleteControlReachesTheRetainedLogTests
{
    private static (CampaignStore Campaigns, RetainedLogStore Logs, CampaignDeletion Deletion) Fixture()
    {
        var campaigns = new CampaignStore(new FakeCampaignArchive(), new RecordingCampaignLog());
        var logs = new RetainedLogStore(new InMemoryLogArchive());
        return (campaigns, logs, new CampaignDeletion(campaigns, logs));
    }

    private static void RetainALogFor(RetainedLogStore logs, Guid campaignId) =>
        logs.Retain(
            new RetainedLog(campaignId, 100, [new LoggedEntry(new LoggedStamp(1, 100), "message", "BCDFGH", "hi")]),
            isHosting: true);

    [Fact]
    public void DeletingACampaignThroughTheControlDeletesItsRetainedLog()
    {
        var (campaigns, logs, deletion) = Fixture();
        var campaign = campaigns.Create(null);
        RetainALogFor(logs, campaign.CampaignId);

        // THE PREMISE, ASSERTED. Without this the test below passes against a build that never
        // retained anything -- "the log is gone" is trivially true of a log that never existed.
        Assert.True(logs.Has(campaign.CampaignId), "No log was retained, so deleting one proves nothing.");

        Assert.True(deletion.Delete(campaign.CampaignId));

        Assert.False(logs.Has(campaign.CampaignId));
        Assert.Null(campaigns.Find(campaign.CampaignId));
    }

    // THE NEGATIVE THE TICKET REQUIRES. Without it, a build that wiped every retained log on any
    // deletion would pass every other assertion in this file.
    [Fact]
    public void AnotherCampaignsLogIsNotTouched()
    {
        var (campaigns, logs, deletion) = Fixture();
        var deleted = campaigns.Create(null);
        var bystander = campaigns.Create(null);
        RetainALogFor(logs, deleted.CampaignId);
        RetainALogFor(logs, bystander.CampaignId);

        Assert.NotEqual(deleted.CampaignId, bystander.CampaignId);

        deletion.Delete(deleted.CampaignId);

        Assert.False(logs.Has(deleted.CampaignId));
        Assert.True(logs.Has(bystander.CampaignId), "An unrelated campaign's log was destroyed.");
    }

    // THE ORPHAN CASE: a retained log whose campaign no longer exists must still be reachable, or it
    // outlives the only control that could remove it.
    //
    // AND IT IS NOT THE GUARD ON | VERSUS ||, THOUGH AN EARLIER COMMENT HERE CLAIMED IT WAS.
    // Mutating | to || leaves this test GREEN: a missing campaign returns false, so || evaluates the
    // log side anyway. The operator is guarded by the ORDINARY deletion cases above, where the
    // campaign delete returns true and || would skip the log. Recorded because the mutation run is
    // what disproved the comment -- the code was right and its stated reason was wrong.
    [Fact]
    public void ALogWhoseCampaignIsAlreadyGoneIsStillDeleted()
    {
        var (campaigns, logs, deletion) = Fixture();
        var orphaned = Guid.NewGuid();
        RetainALogFor(logs, orphaned);

        Assert.Null(campaigns.Find(orphaned));
        Assert.True(logs.Has(orphaned));

        Assert.True(deletion.Delete(orphaned), "Nothing was reported deleted, so the log was skipped.");
        Assert.False(logs.Has(orphaned));
    }

    [Fact]
    public void DeletingNothingReportsNothing()
    {
        var (_, _, deletion) = Fixture();

        Assert.False(deletion.Delete(Guid.NewGuid()));
    }

    private sealed class InMemoryLogArchive : IRetainedLogArchive
    {
        private readonly Dictionary<Guid, string> _logs = [];

        public IReadOnlyList<Guid> Campaigns() => [.. _logs.Keys];

        public string? Read(Guid campaignId) => _logs.GetValueOrDefault(campaignId);

        public void Write(Guid campaignId, string contents) => _logs[campaignId] = contents;

        public bool Delete(Guid campaignId) => _logs.Remove(campaignId);
    }
}
