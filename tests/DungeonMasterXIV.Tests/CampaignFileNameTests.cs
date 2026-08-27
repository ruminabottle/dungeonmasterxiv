using System;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class CampaignFileNameTests
{
    [Fact]
    public void AGeneratedNameRoundTripsBackToItsCampaignId()
    {
        var campaignId = Guid.NewGuid();

        var name = CampaignFileName.NameFor(campaignId);

        Assert.True(CampaignFileName.TryCampaignIdOf(name, out var recovered));
        Assert.Equal(campaignId, recovered);
    }

    // These names reach file writes and deletes. Each is a concrete input that, without the guard,
    // resolves to something outside the campaign set.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("campaigns.json")]
    [InlineData("dalamudUI.ini")]
    [InlineData("campaign-.json")]
    [InlineData("campaign-not-a-uuid.json")]
    [InlineData("../campaign-2f1d5b8e-0000-4000-8000-000000000001.json")]
    [InlineData("campaign-2f1d5b8e-0000-4000-8000-000000000001.json/../../outside.txt")]
    [InlineData("sub\\campaign-2f1d5b8e-0000-4000-8000-000000000001.json")]
    [InlineData("campaign-2f1d5b8e-0000-4000-8000-000000000001.json.bak")]
    public void NamesThisPluginDidNotWriteAreRefused(string? name)
    {
        Assert.False(CampaignFileName.IsCampaignFileName(name));
    }

    [Fact]
    public void TheLegacyNameIsNotMistakenForACampaignFile()
    {
        Assert.False(CampaignFileName.IsCampaignFileName(CampaignFileName.LegacyFileName));
    }
}
