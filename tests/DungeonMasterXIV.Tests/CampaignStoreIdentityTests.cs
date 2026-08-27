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
        Assert.Contains(here.ParticipantId.ToString(), archive.Files[CampaignFileName.NameFor(first.CampaignId)]);
        Assert.Contains(there.ParticipantId.ToString(), archive.Files[CampaignFileName.NameFor(second.CampaignId)]);
    }

    // THE PIN THAT WAS HERE HAS BEEN REMOVED, BECAUSE THE CHANGE IT WAS WAITING FOR IS THIS ONE.
    //
    // ASharedLabelStillLinksAPersonAcrossTwoSessionCodes asserted the limitation that one
    // campaigns.json held every campaign, so a person appearing twice under the same label was
    // correlatable across two codes from a single file. It was written to fail when that was fixed,
    // and it carried a note saying that its failure would be the notification rather than a
    // regression. C10 is that fix, so the pin comes out and this asserts the new property instead.
    //
    // What is asserted is A-1.11b: no single campaign file contains more than one session code.
    // Note what is deliberately NOT claimed. This does not deliver A-1.11 -- two files in one
    // folder, each naming the same person under a different code, link that person exactly as well
    // as one file did, because people zip folders rather than files. A-1.11 was rescoped on
    // 2026-08-27 to cover what leaves the machine. The honest benefit here is narrower: attaching
    // ONE file to a bug report discloses one campaign.
    [Fact]
    public void NoSingleCampaignFileContainsMoreThanOneSessionCode()
    {
        const string SharedLabel = "Yshtola Rhul";
        var store = NewStore(out var archive);
        var first = store.Create(SessionCode.FromValid("BKD7RM"));
        var second = store.Create(SessionCode.FromValid("XW2P4N"));
        store.AddParticipant(first.CampaignId, SharedLabel);
        store.AddParticipant(second.CampaignId, SharedLabel);

        var firstFile = archive.Files[CampaignFileName.NameFor(first.CampaignId)];
        var secondFile = archive.Files[CampaignFileName.NameFor(second.CampaignId)];

        // Each file carries its own code and not the other's. Asserting the ABSENCE as well as the
        // presence is the point -- "contains BKD7RM" alone is satisfied by a file containing both.
        Assert.Contains("BKD7RM", firstFile);
        Assert.DoesNotContain("XW2P4N", firstFile);
        Assert.Contains("XW2P4N", secondFile);
        Assert.DoesNotContain("BKD7RM", secondFile);

        // The label is still in both, and that is not a defect: D-8 permits real character names in
        // the DM's own local history, and A-1.11 no longer covers files that stay on the machine.
        Assert.Contains(SharedLabel, firstFile);
        Assert.Contains(SharedLabel, secondFile);
    }

}
