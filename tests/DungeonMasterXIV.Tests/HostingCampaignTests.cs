using System;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.9i and A-1.9j: hosting always has a campaign, and a prior one is resumable.
/// </summary>
/// <remarks>
/// <b>The gap these close was invisible because nothing failed.</b> <c>Plugin.cs</c> built a store
/// and a coordinator and never connected them, so <c>AddParticipant</c> had zero production callers
/// and no session had a campaign — with no error anywhere, because nothing asked.
/// </remarks>
public class HostingCampaignTests
{
    private static HostingCampaign WithStore(out CampaignStore store)
    {
        store = new CampaignStore(new FakeCampaignArchive(), new RecordingCampaignLog());
        return new HostingCampaign(store);
    }

    // A-1.9i, the whole of it: no choice made, hosting proceeds, and the session HAS a campaign.
    [Fact]
    public void HostingWithNoChoiceMadeStillGetsACampaign()
    {
        var hosting = WithStore(out var store);

        var campaign = hosting.StartFor();

        Assert.NotNull(campaign);
        Assert.Equal(campaign.CampaignId, hosting.Current!.CampaignId);
        Assert.Contains(store.Campaigns, c => c.CampaignId == campaign.CampaignId);
    }

    // A-1.9j: prior campaigns are offered. Empty on a first run, which is what keeps hosting one
    // action for a DM with nothing to resume — the control is ABSENT rather than empty.
    [Fact]
    public void PriorCampaignsAreResumableAndAFirstRunOffersNone()
    {
        var hosting = WithStore(out var store);

        Assert.Empty(hosting.Resumable);

        store.Create(null);

        Assert.Single(hosting.Resumable);
    }

    // Choosing one resumes it rather than creating another — the defect the Spec Owner named:
    // "a DM resuming last week's game silently gets a NEW campaign and loses the roster."
    [Fact]
    public void ChoosingAPriorCampaignResumesItRatherThanCreatingAnother()
    {
        var hosting = WithStore(out var store);
        var lastWeek = store.Create(null);
        lastWeek.Participants.Add(new CampaignParticipant { ParticipantId = Guid.NewGuid() });
        store.Save(lastWeek);

        hosting.Chosen = lastWeek.CampaignId;
        var resumed = hosting.StartFor();

        Assert.Equal(lastWeek.CampaignId, resumed.CampaignId);
        Assert.Single(store.Campaigns);
        Assert.Single(resumed.Participants);
    }

    // A-1.9i is stronger than "usually works": a choice that has gone stale must not refuse the
    // host. Deleting the picked campaign and hosting anyway is the case where the obvious
    // implementation throws or returns null, and REFUSING FAILS the criterion.
    [Fact]
    public void AChoiceThatNoLongerExistsFallsBackToANewCampaignRatherThanRefusing()
    {
        var hosting = WithStore(out var store);
        var gone = store.Create(null);
        hosting.Chosen = gone.CampaignId;
        store.Delete(gone.CampaignId);

        var campaign = hosting.StartFor();

        Assert.NotNull(campaign);
        Assert.NotEqual(gone.CampaignId, campaign.CampaignId);
    }

    // Two sessions in a row without choosing are two DIFFERENT campaigns, not one reused. Reusing
    // would silently merge two unrelated games' rosters, which is the mirror of the defect above.
    [Fact]
    public void HostingTwiceWithoutChoosingCreatesTwoCampaigns()
    {
        var hosting = WithStore(out var store);

        var first = hosting.StartFor();
        hosting.Ended();
        var second = hosting.StartFor();

        Assert.NotEqual(first.CampaignId, second.CampaignId);
        Assert.Equal(2, store.Campaigns.Count);
    }

    // Ending a session forgets the ASSOCIATION and keeps the CAMPAIGN. R-1.6 stores participants on
    // the DM's machine; a session ending must not take them with it.
    [Fact]
    public void EndingTheSessionKeepsTheCampaign()
    {
        var hosting = WithStore(out var store);
        var campaign = hosting.StartFor();

        hosting.Ended();

        Assert.Null(hosting.Current);
        Assert.Contains(store.Campaigns, c => c.CampaignId == campaign.CampaignId);
    }

    // >>> R-1.5c's first half. Until this existed, AddParticipant had ZERO production callers, so a
    // campaign was never anything but a name and a date — and the picker offered a DM continuity the
    // build could not deliver. A campaign IS its roster: Campaign holds no notes and no encounter
    // state, so an empty participant list is an empty campaign.
    [Fact]
    public void AnAdmittedPlayerIsRecordedInTheRunningCampaign()
    {
        var hosting = WithStore(out var store);
        hosting.StartFor();

        var participant = hosting.Record("Ysera");

        Assert.NotNull(participant);
        Assert.Equal("Ysera", Assert.Single(store.Campaigns[0].Participants).Label);
        Assert.NotEqual(Guid.Empty, participant!.ParticipantId);
    }

    // THE POINT OF THE WHOLE CHAIN: resume last week's campaign and the roster is still there. This
    // is the assertion that would have failed before, and it is what makes the resume offer honest
    // rather than a named empty shell.
    [Fact]
    public void AResumedCampaignStillHasLastWeeksRoster()
    {
        var hosting = WithStore(out var store);
        var lastWeek = hosting.StartFor();
        hosting.Record("Ysera");
        hosting.Record("Bob");
        hosting.Ended();

        hosting.Chosen = lastWeek.CampaignId;
        var resumed = hosting.StartFor();

        Assert.Equal(2, resumed.Participants.Count);
        Assert.Contains(resumed.Participants, p => p.Label == "Ysera");
    }

    // Two people with the SAME name are two participants. Names are self-declared and two players
    // may share one (A-1.2d), so nothing may de-duplicate by label — the id is the identity.
    [Fact]
    public void TwoPlayersSharingANameAreTwoParticipants()
    {
        var hosting = WithStore(out var store);
        hosting.StartFor();

        var first = hosting.Record("Bob");
        var second = hosting.Record("Bob");

        Assert.NotEqual(first!.ParticipantId, second!.ParticipantId);
        Assert.Equal(2, store.Campaigns[0].Participants.Count);
    }

    // D-8: the id is minted per campaign and derived from nothing, so the same person appearing in
    // two campaigns is uncorrelated across them.
    [Fact]
    public void TheSamePersonInTwoCampaignsGetsUnrelatedIds()
    {
        var hosting = WithStore(out _);
        hosting.StartFor();
        var inFirst = hosting.Record("Ysera");
        hosting.Ended();
        hosting.StartFor();
        var inSecond = hosting.Record("Ysera");

        Assert.NotEqual(inFirst!.ParticipantId, inSecond!.ParticipantId);
    }

    // A resolved relink records NOTHING — that person already has a participant here. Currently
    // unreachable (nothing tells a joiner its id yet), and guarded anyway: without it, a returning
    // player would silently acquire a SECOND participant the moment relink starts working, and the
    // defect would arrive with somebody else's change and look like theirs.
    [Fact]
    public void AResolvedRelinkAddsNoSecondParticipant()
    {
        var hosting = WithStore(out var store);
        hosting.StartFor();
        hosting.Record("Ysera");

        Assert.Null(hosting.Record("Ysera", alreadyAParticipant: true));
        Assert.Single(store.Campaigns[0].Participants);
    }

    // An admission with no running session records nothing and does not throw. Not a state this can
    // fix, and it must not take the session down.
    [Fact]
    public void RecordingWithNoRunningSessionDoesNothing()
    {
        var hosting = WithStore(out var store);

        Assert.Null(hosting.Record("Ysera"));
        Assert.Empty(store.Campaigns);
    }

    // The campaign a session starts under carries no code, because the code is not yet true —
    // R-1.2a lets it change, and recording one here would record a value the session has not claimed.
    [Fact]
    public void ANewCampaignStartsWithNoPreferredCode()
    {
        var hosting = WithStore(out _);

        Assert.Null(hosting.StartFor().PreferredCode);
    }
}
