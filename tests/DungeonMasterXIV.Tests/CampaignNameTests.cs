using System;
using System.Globalization;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.9k and SQ-54: what an auto-created campaign calls itself.
/// </summary>
/// <remarks>
/// <b>Every property here was RULED rather than chosen</b>, so each test names the ruling it pins.
/// The one judgement inside the implementation — dropping the weekday — is pinned too, because it
/// is the part a later reader is most likely to assume was arbitrary.
/// </remarks>
public class CampaignNameTests
{
    private static readonly DateTimeOffset Created = new(2026, 8, 28, 20, 14, 0, TimeSpan.Zero);

    private static Campaign At(DateTimeOffset created) =>
        new() { CampaignId = Guid.NewGuid(), CreatedUtc = created };

    // SQ-54: the components and their ORDER are load-bearing; punctuation is not. So this asserts
    // the date parts and a clock time are present and in that order, rather than a literal string —
    // a literal would fail on a machine whose culture punctuates differently, which is exactly the
    // property the ruling protects.
    [Fact]
    public void TheAutoNameIsTheCreationDateThenTheClockTime()
    {
        var name = CampaignName.Auto(Created, CultureInfo.GetCultureInfo("en-GB"));

        Assert.Contains("2026", name, StringComparison.Ordinal);
        Assert.Contains("August", name, StringComparison.Ordinal);
        Assert.True(
            name.IndexOf("August", StringComparison.Ordinal) < name.IndexOf(":", StringComparison.Ordinal),
            $"The date must precede the clock time. Got '{name}'.");
    }

    // SQ-54, the Product Owner's ruling and the reason it overruled a prefix: a campaign is NOT a
    // session, and "Session of ..." would be the one place the product conflates them — teaching the
    // wrong model to the person who most needs the right one. It is also accurate only at creation,
    // becoming a misnomer the moment the campaign is RESUMED, which is when the feature has worked.
    [Fact]
    public void TheAutoNameCarriesNoSessionPrefix()
    {
        var name = CampaignName.Auto(Created, CultureInfo.GetCultureInfo("en-GB"));

        Assert.DoesNotContain("session", name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("campaign", name, StringComparison.OrdinalIgnoreCase);
    }

    // The judgement call, pinned so it is visible rather than assumed arbitrary. SQ-54's draft
    // carries no weekday and several cultures put one in their long date pattern, so it is removed
    // — otherwise the name would carry a component for some readers and not others.
    [Fact]
    public void TheAutoNameCarriesNoWeekday()
    {
        foreach (var tag in new[] { "en-US", "en-GB", "de-DE", "fr-FR" })
        {
            var culture = CultureInfo.GetCultureInfo(tag);
            var name = CampaignName.Auto(Created, culture);

            foreach (var weekday in culture.DateTimeFormat.DayNames)
            {
                Assert.DoesNotContain(weekday, name, StringComparison.CurrentCultureIgnoreCase);
            }
        }
    }

    // A-1.9k-2, the culture property: rendered in the DM's own culture AT READ TIME. This is what
    // makes storing a formatted string wrong — the same instant must read differently for different
    // readers, which a frozen string cannot do.
    [Fact]
    public void TheSameInstantRendersDifferentlyForDifferentCultures()
    {
        var british = CampaignName.Auto(Created, CultureInfo.GetCultureInfo("en-GB"));
        var german = CampaignName.Auto(Created, CultureInfo.GetCultureInfo("de-DE"));

        Assert.NotEqual(british, german);
    }

    // A-1.9k-1: distinctness is NOT required. Two campaigns created in the same minute share a name
    // and that is ALLOWED — the criterion asks for identifiable, not unique, and renaming is the
    // escape hatch. Asserted rather than left implicit, because a later reader "fixing" this by
    // adding a disambiguating suffix would push the name back toward the id-shaped thing A-1.9k
    // rejects. This test is what tells them it was deliberate.
    [Fact]
    public void TwoCampaignsCreatedTogetherMayShareAName()
    {
        Assert.Equal(CampaignName.For(At(Created)), CampaignName.For(At(Created)));
    }

    // A-1.9k-4's mechanism at the unit level: the name is derived from an instant that never
    // changes, so nothing a session does can move it.
    [Fact]
    public void TheAutoNameDoesNotDependOnTheCampaignsCode()
    {
        var campaign = At(Created);
        var before = CampaignName.For(campaign);

        campaign.PreferredCode = "BKD7RM";

        Assert.Equal(before, CampaignName.For(campaign));
    }

    // An empty or whitespace rename is not a rename — it would render the empty label A-1.9k rules
    // out, so it falls back rather than being shown.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyStoredNameFallsBackToTheAutomaticOne(string? stored)
    {
        var campaign = At(Created);
        campaign.Name = stored;

        Assert.Equal(CampaignName.Auto(Created), CampaignName.For(campaign));
    }
}
