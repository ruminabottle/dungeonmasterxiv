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

    [Fact]
    public void ACampaignIsLabelledByItsCodeInTheFormItIsReadAloud()
    {
        var rows = CampaignListView.Build(new List<Campaign> { WithCode("BKD7RM") });

        Assert.Equal("BKD-7RM", Assert.Single(rows).Label);
    }

    [Fact]
    public void ACampaignThatHasNeverBeenHostedSaysSoRatherThanShowingNothing()
    {
        var rows = CampaignListView.Build(new List<Campaign> { WithCode(null) });

        Assert.Equal(CampaignListView.NoCodeLabel, Assert.Single(rows).Label);
    }

    [Fact]
    public void AStoredCodeThatIsNoLongerValidFallsBackRatherThanRenderingGarbage()
    {
        var rows = CampaignListView.Build(new List<Campaign> { WithCode("AEIOU!") });

        Assert.Equal(CampaignListView.NoCodeLabel, Assert.Single(rows).Label);
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
