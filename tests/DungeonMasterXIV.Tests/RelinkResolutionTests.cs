using System;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Resolving a claimed participant against the campaign's own records (R-1.5).
/// </summary>
/// <remarks>
/// These are the rejection cases. They are the ones that <i>look</i> like the security tests, and
/// they are not the important ones — a relink that auto-admits on a valid match passes every test
/// in this file. That case lives in <c>RelinkRequiresApprovalTests</c>.
/// </remarks>
public class RelinkResolutionTests
{
    private static Campaign ACampaignKnowing(Guid participantId, string label = "Ysera")
    {
        var campaign = new Campaign { CampaignId = Guid.NewGuid() };
        campaign.Participants.Add(new CampaignParticipant { ParticipantId = participantId, Label = label });
        return campaign;
    }

    [Fact]
    public void AParticipantThisCampaignKnowsResolves()
    {
        var participantId = Guid.NewGuid();

        var claim = CampaignRelink.Resolve(ACampaignKnowing(participantId), participantId.ToString("D"));

        Assert.True(claim.Matched);
        Assert.Equal("Ysera", claim.Label);
    }

    // The label the DM reads must come from the STORE, not from the request. Precondition 12: derive
    // what is shown from what resolved, never from what was claimed. There is no path for a caller
    // to supply a label here at all, which is the point -- Resolve takes an id and nothing else.
    [Fact]
    public void TheLabelComesFromTheStoredParticipant()
    {
        var participantId = Guid.NewGuid();
        var campaign = ACampaignKnowing(participantId, "The name the DM gave them");

        var claim = CampaignRelink.Resolve(campaign, participantId.ToString("D"));

        Assert.Equal("The name the DM gave them", claim.Label);
    }

    [Fact]
    public void AParticipantFromADifferentCampaignDoesNotResolve()
    {
        var stranger = Guid.NewGuid();

        var claim = CampaignRelink.Resolve(ACampaignKnowing(Guid.NewGuid()), stranger.ToString("D"));

        Assert.False(claim.Matched);
        Assert.Null(claim.Label);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("2f1d5b8e-0000-4000-8000")]
    [InlineData("{2f1d5b8e-0000-4000-8000-000000000001}")]
    [InlineData("2f1d5b8e00004000800000000000001")]
    public void AClaimThatIsNotAPlainUuidDoesNotResolve(string? claimed)
    {
        var claim = CampaignRelink.Resolve(ACampaignKnowing(Guid.NewGuid()), claimed);

        Assert.False(claim.Matched);
        Assert.Null(claim.Label);
    }

    [Fact]
    public void NoCampaignMeansNothingResolves()
    {
        var claim = CampaignRelink.Resolve(null, Guid.NewGuid().ToString("D"));

        Assert.False(claim.Matched);
    }

    [Fact]
    public void ACampaignWithNoParticipantsResolvesNothing()
    {
        var campaign = new Campaign { CampaignId = Guid.NewGuid() };

        var claim = CampaignRelink.Resolve(campaign, Guid.NewGuid().ToString("D"));

        Assert.False(claim.Matched);
    }

    // A claim that fails to resolve must be indistinguishable from no claim at all, so that a
    // failed guess cannot be told apart from an ordinary join by watching the prompt.
    [Fact]
    public void AFailedClaimIsTheSameAsNoClaim()
    {
        var failed = CampaignRelink.Resolve(ACampaignKnowing(Guid.NewGuid()), Guid.NewGuid().ToString("D"));

        Assert.Equal(RelinkClaim.None, failed);
    }
}
