using System;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The store's primary key, and the identity guarantees that hang off it. R-1.2a and R-1.6 both
/// come down to one thing: a campaign is its UUID, and a session code is only a label.
/// </summary>
public class CampaignStoreIdentityTests
{
    private static CampaignStore NewStore(out FakeCampaignArchive archive)
    {
        archive = new FakeCampaignArchive();
        return new CampaignStore(archive, new RecordingCampaignLog());
    }

    [Fact]
    public void TwoCampaignsMayShareAPreferredCodeAndRemainSeparateCampaigns()
    {
        // The defect this whole chunk was flagged for. If anything keyed on the code, the second
        // Create would collide with, overwrite or return the first.
        var store = NewStore(out _);
        var code = SessionCode.FromValid("BKD7RM");

        var first = store.Create(code);
        var second = store.Create(code);

        Assert.NotEqual(first.CampaignId, second.CampaignId);
        Assert.Equal(2, store.Campaigns.Count);
        Assert.NotNull(store.Find(first.CampaignId));
        Assert.NotNull(store.Find(second.CampaignId));
    }

    [Fact]
    public void ACodeTakenAtResumeCostsANewCodeAndNotTheCampaign()
    {
        // R-1.2a's resume paragraph, as a test. The DM comes back, their usual code is gone, they
        // take another one — and the campaign, with everyone in it, is still theirs.
        var store = NewStore(out _);
        var campaign = store.Create(SessionCode.FromValid("BKD7RM"));

        // Snapshot the id BEFORE relabelling. Reading it back off `campaign` afterwards would
        // compare the campaign against itself: a store that re-keyed on the new code would mutate
        // that same object and the assertion would follow it and pass. Found by substituting
        // exactly that defect, which this test survived until the snapshot was taken.
        var campaignIdBefore = campaign.CampaignId;
        store.AddParticipant(campaignIdBefore, "Yshtola Rhul");
        var participantId = campaign.Participants.Single().ParticipantId;

        var relabelled = store.SetPreferredCode(campaignIdBefore, SessionCode.FromValid("XW2P4N"));

        Assert.True(relabelled);
        Assert.Single(store.Campaigns);
        var reloaded = store.Find(campaignIdBefore);
        Assert.NotNull(reloaded);
        Assert.Equal(campaignIdBefore, reloaded!.CampaignId);
        Assert.Equal("XW2P4N", reloaded.PreferredCode);
        Assert.Equal(participantId, reloaded.Participants.Single().ParticipantId);
    }

    [Fact]
    public void ACampaignIdentityIsNotDerivedFromItsCode()
    {
        var store = NewStore(out _);

        var campaign = store.Create(SessionCode.FromValid("BKD7RM"));

        Assert.NotEqual(Guid.Empty, campaign.CampaignId);
        Assert.DoesNotContain("BKD7RM", campaign.CampaignId.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACampaignMayExistBeforeItHasEverBeenHosted()
    {
        var store = NewStore(out _);

        var campaign = store.Create(preferredCode: null);

        Assert.Null(campaign.PreferredCode);
        Assert.NotEqual(Guid.Empty, campaign.CampaignId);
    }

    // Named for exactly what it checks: IDENTIFIERS are not shared. That is necessary for A-1.11
    // and not sufficient for it, and this test does not claim otherwise — see
    // ASharedLabelStillLinksAPersonAcrossTwoSessionCodes below for the half that does not hold.
    //
    // Deliberately uses two DIFFERENT labels. The previous version of this test gave both
    // participants the same label, which put every ingredient of an A-1.11 violation into the
    // fixture and then asserted only that the UUIDs differed — so a reader saw two codes and one
    // label and concluded the property was under test. Building the counterexample and measuring
    // something else is worse than not building it, because the fixture does the arguing.
    [Fact]
    public void ParticipantIdentifiersAreNeverSharedBetweenCampaigns()
    {
        // Fails the moment a participant id is derived from the label, or reused across campaigns.
        var store = NewStore(out var archive);
        var first = store.Create(SessionCode.FromValid("BKD7RM"));
        var second = store.Create(SessionCode.FromValid("XW2P4N"));

        var here = store.AddParticipant(first.CampaignId, "Yshtola Rhul");
        var there = store.AddParticipant(second.CampaignId, "Thancred Waters");

        Assert.NotNull(here);
        Assert.NotNull(there);
        Assert.NotEqual(here!.ParticipantId, there!.ParticipantId);

        // And the same is true of what actually reaches disk, not just of the objects in memory.
        var written = archive.Content;
        Assert.NotNull(written);
        Assert.Contains(here.ParticipantId.ToString(), written!);
        Assert.Contains(there.ParticipantId.ToString(), written!);
    }

    // This test asserts a LIMITATION, not a guarantee, and it exists so the limitation cannot be
    // forgotten.
    //
    // One campaigns.json holds every campaign, each carrying its own PreferredCode, and Label is
    // not rotated — so a person appearing in two campaigns under the same label is correlatable
    // across two codes from that single file.
    //
    // WHICH REQUIREMENT THAT OFFENDS CHANGED ON 2026-08-27, so the citation here is deliberate.
    // A-1.11 was narrowed to cover only what LEAVES the machine, because as written it contradicted
    // D-8's explicit permission for local history to hold character names. This file never leaves
    // the machine, so it does not offend A-1.11. It offends A-1.11b — no single campaign file
    // contains more than one session code — which was added for the honest reason rather than the
    // rhetorical one: it bounds blast radius when someone zips a folder into a bug report. The
    // Product Owner rejected per-campaign files as a way of making A-1.11 literally true, on the
    // grounds that two files in one folder link a person exactly as well as one file does.
    //
    // WHEN THE A-1.11b RESTRUCTURE LANDS THIS TEST FAILS, and that failure is the notification
    // working: it means the pin should be removed, not that something regressed. Verified to fire
    // by substituting the fix — see the PR.
    [Fact]
    public void ASharedLabelStillLinksAPersonAcrossTwoSessionCodes()
    {
        const string SharedLabel = "Yshtola Rhul";
        var store = NewStore(out var archive);
        var first = store.Create(SessionCode.FromValid("BKD7RM"));
        var second = store.Create(SessionCode.FromValid("XW2P4N"));
        store.AddParticipant(first.CampaignId, SharedLabel);
        store.AddParticipant(second.CampaignId, SharedLabel);

        var written = archive.Content;

        Assert.NotNull(written);
        Assert.Contains("BKD7RM", written!);
        Assert.Contains("XW2P4N", written!);
        Assert.Contains(SharedLabel, written!);
    }

    [Fact]
    public void AddingToACampaignThatDoesNotExistChangesNothing()
    {
        var store = NewStore(out var archive);

        var participant = store.AddParticipant(Guid.NewGuid(), "Nobody");

        Assert.Null(participant);
        Assert.Empty(archive.Writes);
    }
}
