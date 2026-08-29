using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// SQ-87: a display name stored BEFORE names were campaign-scoped survives the upgrade as a local
/// pre-fill default — <b>offered, never applied, and never sent unaccepted</b> (A-2.32, A-2.33).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT THIS PINS.</b> Campaign-scoping removed <c>PluginSettings.DisplayNameAlias</c> and
/// shipped no migration, so Dalamud deserialised a v0.1.5 config into a type that no longer declared
/// the property, the value was dropped, and the next save wrote it away. <b>The window closed at the
/// first save</b>, and the existing schema hook could not see the upgrade at all: a v0.1.5 file
/// carries version 1, this build still writes 1, and <c>RequiresWriteOnLoad</c> fires only when
/// nothing readable loaded.
/// </para>
/// <para>
/// <b>A-2.32 AND A-2.33 ARE PINNED SEPARATELY BECAUSE THEY FAIL IN OPPOSITE DIRECTIONS AND EACH
/// ONE'S PASSING TEST IS BLIND TO THE OTHER'S FAILURE.</b> A-2.32 is over PROVENANCE rather than over
/// the string, so its fixture gives the accepted name and the carried-over default <b>different
/// values</b> — a test that asserted only <i>"the right string left the client"</i> would pass a
/// build that sent the stored default, because two identical byte sequences cannot record where they
/// came from. A-2.33 is the twin from the other side: a build that pre-fills correctly and sends
/// without showing satisfies A-2.32 and fails this.
/// </para>
/// <para>
/// <b>What is machine-checkable here and what is not, stated rather than implied.</b> The PRD marks
/// A-2.33 <i>"machine for the send, in-game for the player having seen it"</i>. These tests cover the
/// SEND half — that merely offering a name cannot cause it to be sent. <b>Whether the box was
/// actually drawn is not observable from this project</b>, because no test project links the plugin.
/// </para>
/// </remarks>
public class ANameFromBeforeCampaignScopingBecomesAPreFillDefaultTests
{
    private static readonly DisplayName CharacterName = DisplayName.OrNone("Y'shtola Rhul");

    /// <summary>What a v0.1.5 file carries: a name chosen before campaigns existed.</summary>
    private const string CarriedOver = "The Cartographer";

    /// <summary>A name accepted IN a campaign, deliberately different from the carried-over one.</summary>
    private const string AcceptedHere = "Renn of the Ninth";

    // THE FIXTURE'S OWN PREMISE, ASSERTED RATHER THAN ASSUMED. Every provenance test below can only
    // distinguish accepted-here from carried-over while these three differ. If a later edit makes two
    // of them equal, the tests keep passing and stop meaning anything -- so this fails loudly first.
    [Fact]
    public void TheFixtureCanTellTheThreeNamesApart()
    {
        Assert.NotEqual(CarriedOver, AcceptedHere);
        Assert.NotEqual(CarriedOver, CharacterName.Value);
        Assert.NotEqual(AcceptedHere, CharacterName.Value);
    }

    // ---- recovery: the value is still there to be offered.

    /// <summary>
    /// The heart of the ticket: the alias is recovered from the JSON that is ALREADY on disk, by
    /// name, with no version comparison and no migration step.
    /// </summary>
    /// <remarks>
    /// <b>Deserialised with Newtonsoft because that is what Dalamud persists settings with</b>, and a
    /// round trip through a different serializer would test a document nothing writes. The key is
    /// <c>DisplayNameAlias</c> because that is literally what v0.1.5 wrote —
    /// <c>v0.1.5:src/DungeonMasterXIV.Core/Data/PluginSettings.cs:150</c>, a string defaulting to
    /// empty. <b>The unknown member below is deliberate</b>: a real v0.1.5 document contains keys this
    /// build no longer declares, and recovery must survive them rather than throw.
    /// </remarks>
    [Fact]
    public void TheNameIsRecoveredFromAv015DocumentWithoutAMigrationStep()
    {
        var onDisk =
            """
            {
              "DisplayNameAlias": "The Cartographer",
              "MainWindowOpen": true,
              "SomeKeyThisBuildNoLongerDeclares": "ignored"
            }
            """;

        var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<PluginSettings>(onDisk)!;

        Assert.Equal(CarriedOver, settings.DisplayNameAlias);
        Assert.True(settings.MainWindowOpen, "An unknown member must not stop the rest of the document loading.");
    }

    // THE BYSTANDER, AND WITHOUT IT "recovered" IS SATISFIED BY RETURNING THE SAME STRING ALWAYS. A
    // client that never ran v0.1.5 has no carried-over name and must behave exactly as before.
    [Fact]
    public void AClientThatNeverRanV015CarriesNothing()
    {
        var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<PluginSettings>("{}")!;

        Assert.Equal(string.Empty, settings.DisplayNameAlias);
        Assert.Equal(CharacterName.Value, CampaignDisplayName.ToEdit(null, settings.DisplayNameAlias, CharacterName));
    }

    // ---- offered.

    [Fact]
    public void TheCarriedOverNameIsWhatTheBoxOffersWhenTheCampaignHasNoneOfItsOwn()
    {
        var campaign = new Campaign();

        Assert.Equal(CarriedOver, CampaignDisplayName.ToEdit(campaign, CarriedOver, CharacterName));
    }

    [Fact]
    public void ANameAcceptedInThisCampaignBeatsTheCarriedOverDefault()
    {
        var campaign = new Campaign();
        CampaignDisplayName.RecordChosen(campaign, AcceptedHere, CharacterName);

        // A considered choice must not be overwritten by a leftover every time the box is opened.
        Assert.Equal(AcceptedHere, CampaignDisplayName.ToEdit(campaign, CarriedOver, CharacterName));
    }

    // ---- A-2.32: the carried-over default never travels as itself.

    /// <summary>
    /// A-2.32. What leaves the client is what the player accepted IN THIS CAMPAIGN, not the stored
    /// value — asserted so that a build sending the stored default fails.
    /// </summary>
    /// <remarks>
    /// <b>The second assertion is the criterion and the first is not.</b> Asserting only that
    /// <c>AcceptedHere</c> is sent would also pass a build that sends the carried-over value in some
    /// OTHER campaign, so the carried-over string is named explicitly as a thing that must not appear.
    /// </remarks>
    [Fact]
    public void WhatIsSentIsWhatThePlayerAcceptedHereAndNotTheStoredDefault()
    {
        var campaign = new Campaign();
        CampaignDisplayName.RecordChosen(campaign, AcceptedHere, CharacterName);

        var sent = CampaignDisplayName.Or(campaign, CharacterName).Value;

        Assert.Equal(AcceptedHere, sent);
        Assert.NotEqual(CarriedOver, sent);
    }

    /// <summary>
    /// A-2.32 across two campaigns, which is the form the criterion is written in: <i>"hold a stored
    /// pre-fill default, join two campaigns, and inspect what leaves the client"</i>.
    /// </summary>
    [Fact]
    public void TwoCampaignsSendTwoDifferentThingsAndNeitherIsTheStoredDefault()
    {
        var a = new Campaign();
        CampaignDisplayName.RecordChosen(a, AcceptedHere, CharacterName);

        // B is joined without the player accepting anything, so B sends the character name.
        var b = new Campaign();

        Assert.Equal(AcceptedHere, CampaignDisplayName.Or(a, CharacterName).Value);
        Assert.Equal(CharacterName.Value, CampaignDisplayName.Or(b, CharacterName).Value);
        Assert.NotEqual(CarriedOver, CampaignDisplayName.Or(b, CharacterName).Value);
    }

    // ---- A-2.33: a pre-filled name the player never accepted is never sent.

    /// <summary>
    /// A-2.33's send half. <b>Offering the name is not accepting it</b>: the box is pre-filled with
    /// the carried-over value and nothing is sent under it.
    /// </summary>
    /// <remarks>
    /// <b>The first assertion is what makes the second mean something.</b> Without it, "nothing was
    /// sent under that name" is equally true of a build that never offered the name at all — and that
    /// build fails SQ-87 while passing a test written only on the send.
    /// </remarks>
    [Fact]
    public void ACarriedOverNameThatWasOnlyOfferedIsNotSent()
    {
        var campaign = new Campaign();

        var offered = CampaignDisplayName.ToEdit(campaign, CarriedOver, CharacterName);

        Assert.Equal(CarriedOver, offered);
        Assert.Equal(CharacterName.Value, CampaignDisplayName.Or(campaign, CharacterName).Value);
    }

    /// <summary>
    /// The route from offered to sent, so the test above is not read as "the carried name can never
    /// be used". <b>Acceptance is an act</b>, and RecordChosen is that act.
    /// </summary>
    [Fact]
    public void AcceptingTheOfferedNameIsWhatMakesItSendable()
    {
        var campaign = new Campaign();
        var offered = CampaignDisplayName.ToEdit(campaign, CarriedOver, CharacterName);

        Assert.True(CampaignDisplayName.RecordChosen(campaign, offered, CharacterName));

        Assert.Equal(CarriedOver, CampaignDisplayName.Or(campaign, CharacterName).Value);
    }

    // ---- A-2.30 must survive the carried-over default.

    /// <summary>
    /// A-2.30's second half, re-checked with a carried-over value present. <b>A name chosen in A must
    /// still not be offered in B</b> — the carry-over must not become a back door for exactly the
    /// portability D-8 forbids.
    /// </summary>
    [Fact]
    public void ANameChosenInOneCampaignIsStillNotOfferedInAnotherWhenACarryOverExists()
    {
        var a = new Campaign();
        CampaignDisplayName.RecordChosen(a, AcceptedHere, CharacterName);

        var b = new Campaign();

        var offeredInB = CampaignDisplayName.ToEdit(b, CarriedOver, CharacterName);

        Assert.NotEqual(AcceptedHere, offeredInB);
        Assert.Equal(CarriedOver, offeredInB);
    }
}
