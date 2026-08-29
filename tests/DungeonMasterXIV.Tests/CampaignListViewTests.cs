using System;
using System.Collections.Generic;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class CampaignListViewTests
{
    private static Campaign WithCode(string? code, int participants = 0)
    {
        var campaign = new Campaign
        {
            CampaignId = Guid.NewGuid(),
            PreferredCode = code,
            CreatedUtc = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero),
        };

        for (var i = 0; i < participants; i++)
        {
            campaign.Participants.Add(new CampaignParticipant { ParticipantId = Guid.NewGuid() });
        }

        return campaign;
    }

    // THESE THREE REPLACE TESTS THAT ASSERTED THE OPPOSITE, and the reason is that the REQUIREMENT
    // changed rather than the behaviour drifting. R-1.6 used to call the stored code the campaign's
    // "preferred LABEL"; CampaignListView displayed it, and these tests pinned that faithfully. The
    // Spec Owner corrected the requirement rather than the code — "the implementation is faithful
    // and my requirement was wrong" — so the old assertions now encode a rule that no longer exists.
    // Deleting a green test is normally the wrong move; here the test was the record of a mistake.

    // A-1.9k-3. The code must not appear as a name AT ALL, including in the form it is read aloud.
    [Fact]
    public void ACampaignIsNeverLabelledByItsSessionCode()
    {
        var rows = CampaignListView.Build(new List<Campaign> { WithCode("BKD7RM") });

        var label = Assert.Single(rows).Label;
        Assert.DoesNotContain("BKD", label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BKD-7RM", label, StringComparison.OrdinalIgnoreCase);
    }

    // A-1.9k-3's other half: "(no code yet)" was an EMPTY LABEL in the sense A-1.9k rules out. A
    // campaign that has never been hosted has been created, so it has a name like any other.
    [Fact]
    public void ACampaignThatHasNeverBeenHostedStillHasAName()
    {
        var rows = CampaignListView.Build(new List<Campaign> { WithCode(null) });

        var label = Assert.Single(rows).Label;
        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.NotEqual(CampaignListView.NoCodeLabel, label);
    }

    // A stored code this build cannot parse used to degrade the LABEL. It no longer can, because the
    // label never consults the code — the failure mode is gone rather than handled.
    [Fact]
    public void AnUnparseableStoredCodeCannotAffectTheName()
    {
        var readable = Assert.Single(CampaignListView.Build(new List<Campaign> { WithCode("BKD7RM") })).Label;
        var garbage = Assert.Single(CampaignListView.Build(new List<Campaign> { WithCode("AEIOU!") })).Label;

        Assert.Equal(readable, garbage);
    }

    // >>> A-1.9k-4, THE SHARP ONE. Move a code BETWEEN two campaigns and assert NEITHER NAME MOVED.
    //
    // This catches the SWAP, not merely the stale label, and the swap is what produces a DM
    // confidently resuming the WRONG game. Under the old rule both assertions below failed: the
    // names were the codes, so handing a code over handed the name over with it.
    [Fact]
    public void ACodeMovingBetweenCampaignsMovesNeitherName()
    {
        var mine = WithCode("BKD7RM");
        var yours = WithCode(null);
        yours.CreatedUtc = mine.CreatedUtc.AddDays(-3);

        var mineBefore = CampaignName.For(mine);
        var yoursBefore = CampaignName.For(yours);

        // R-1.2a: a code taken at resume costs a new code, not the campaign. So this is an ordinary
        // event, not an abuse case.
        mine.PreferredCode = null;
        yours.PreferredCode = "BKD7RM";

        Assert.Equal(mineBefore, CampaignName.For(mine));
        Assert.Equal(yoursBefore, CampaignName.For(yours));
        Assert.NotEqual(CampaignName.For(mine), CampaignName.For(yours));
    }

    // A rename wins outright, because R-1.5d makes an auto-created campaign renameable and a display
    // that could override the stored name would not be a rename.
    [Fact]
    public void AStoredNameWinsOverTheAutomaticOne()
    {
        var campaign = WithCode("BKD7RM");
        var automatic = CampaignName.For(campaign);

        campaign.Name = "Tuesday night Ishgard";

        Assert.Equal("Tuesday night Ishgard", CampaignName.For(campaign));
        Assert.NotEqual(automatic, CampaignName.For(campaign));
        Assert.Equal("Tuesday night Ishgard", Assert.Single(CampaignListView.Build(new List<Campaign> { campaign })).Label);
    }

    [Theory]
    [InlineData(0, "0 participants")]
    [InlineData(1, "1 participant")]
    [InlineData(3, "3 participants")]
    public void TheDetailLineCountsParticipants(int count, string expected)
    {
        var rows = CampaignListView.Build(new List<Campaign> { WithCode("BKD7RM", count) });

        Assert.Contains(expected, Assert.Single(rows).Detail);
    }

    [Fact]
    public void RowsForCampaignsSharingACodeStillAddressThemSeparately()
    {
        // The list labels by code, so this is where a code-keyed list would betray itself.
        var first = WithCode("BKD7RM");
        var second = WithCode("BKD7RM");

        var rows = CampaignListView.Build(new List<Campaign> { first, second });

        Assert.Equal(2, rows.Count);
        Assert.Equal(rows[0].Label, rows[1].Label);
        Assert.NotEqual(rows[0].CampaignId, rows[1].CampaignId);
    }
}
