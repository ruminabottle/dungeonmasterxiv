using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>Clearing a campaign's name box is a choice: the old carried-over name is not offered again.</summary>
public sealed class AClearedNameStaysClearedTests
{
    [Fact]
    public void ACampaignNameClearedByThePlayerShowsTheCharacterNameNotTheOldDefault()
    {
        var campaign = new Campaign();
        DisplayName.TryParse("Svafnir Asoltun", out var characterName);

        Assert.Equal("Old Name", CampaignDisplayName.ToPreFill(campaign, "Old Name", characterName));

        Assert.True(CampaignDisplayName.RecordChosen(campaign, string.Empty, characterName));

        Assert.Equal("Svafnir Asoltun", CampaignDisplayName.ToPreFill(campaign, "Old Name", characterName));
    }
}
