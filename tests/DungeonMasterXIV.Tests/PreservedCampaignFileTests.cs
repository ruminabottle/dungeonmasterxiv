using System;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class PreservedCampaignFileTests
{
    [Fact]
    public void AGeneratedNameIsAcceptedByTheGuardThatProtectsDeletion()
    {
        var name = PreservedCampaignFile.NameFor(new DateTimeOffset(2026, 8, 27, 1, 2, 3, TimeSpan.Zero));

        Assert.True(PreservedCampaignFile.IsPreservedName(name));
        Assert.Contains("20260827T010203Z", name);
    }

    // The name reaches a file delete, so the guard has to reject anything carrying a path. Each of
    // these is a concrete input that makes an unguarded implementation delete the wrong file.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("campaigns.json")]
    [InlineData("dalamudUI.ini")]
    [InlineData("campaigns.unreadable-.json")]
    [InlineData("../campaigns.unreadable-x.json")]
    [InlineData("campaigns.unreadable-../../x.json")]
    [InlineData("/etc/campaigns.unreadable-x.json")]
    [InlineData("sub\\campaigns.unreadable-x.json")]
    [InlineData("campaigns.unreadable-x.json.bak")]
    public void NamesThisPluginDidNotWriteAreRefused(string? name)
    {
        Assert.False(PreservedCampaignFile.IsPreservedName(name));
    }
}
