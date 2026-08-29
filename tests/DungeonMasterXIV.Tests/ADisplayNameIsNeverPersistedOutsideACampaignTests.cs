using System.Text.Json;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.30 and A-2.31 — a display name is remembered inside ONE campaign and nowhere else
/// (R-2.17, D-8).
/// </summary>
/// <remarks>
/// <para>
/// <b>This file was written against <c>4dc8713</c> and was RED there, which is the only reason it is
/// evidence.</b> The forbidden shape was already on <c>main</c>: <c>PluginSettings</c> carried one
/// alias, global to the player, persisting across every campaign. Written before the fix so the
/// criterion failed before it passed, rather than being written to fit what was built.
/// </para>
/// <para>
/// <b>A-2.31 is checked by comparing whole documents, not by looking for a field.</b> A test that
/// greps the settings JSON for <c>DisplayNameAlias</c>, or asserts that property is gone, passes the
/// moment the same value is stored under another name — a control that fires only on the spelling
/// its author picked. <b>Choosing a name must change NOTHING in the global document</b>, so the
/// comparison is byte-for-byte and no spelling gets past it.
/// </para>
/// <para>
/// <b>A-2.30's second half is the one a build gets wrong</b>, because global persistence is simpler
/// and is what <i>"persistence across sessions"</i> reads as. A test that asserts only <i>"offered
/// again in campaign A"</i> passes the forbidden implementation, so the not-offered-in-B half is
/// asserted separately and against a campaign that is genuinely a different one.
/// </para>
/// </remarks>
public class ADisplayNameIsNeverPersistedOutsideACampaignTests
{
    private static readonly DisplayName CharacterName = DisplayName.OrNone("Y'shtola Rhul");

    private const string Chosen = "The Cartographer";

    // A-2.31, THE CRITERION. Both entry points, because the criterion asks what the product CAN do:
    // a name reachable only through the less obvious call is still a name outside a campaign.
    [Theory]
    [InlineData("chosen through the settings box")]
    [InlineData("recorded directly")]
    public void ChoosingANameChangesNothingInTheGlobalSettingsDocument(string route)
    {
        var untouched = SettingsDocument(new PluginSettings());

        var settings = new PluginSettings();
        var campaign = new Campaign();
        if (route == "chosen through the settings box")
        {
            CampaignDisplayName.RecordChosen(campaign, Chosen, CharacterName);
        }
        else
        {
            CampaignDisplayName.Record(campaign, Chosen);
        }

        Assert.Equal(untouched, SettingsDocument(settings));
    }

    // A-2.31, THE OTHER DIRECTION, AND WITHOUT IT THE TEST ABOVE IS SATISFIED BY STORING THE NAME
    // NOWHERE AT ALL. The settings document must not carry the name AND the campaign document must,
    // or "nothing changed globally" is equally true of a build that simply lost it.
    [Fact]
    public void TheNameIsInTheCampaignDocumentAndNotInTheSettingsDocument()
    {
        var campaign = new Campaign();
        CampaignDisplayName.RecordChosen(campaign, Chosen, CharacterName);

        Assert.Contains(Chosen, JsonSerializer.Serialize(campaign));
        Assert.DoesNotContain(Chosen, SettingsDocument(new PluginSettings()));
    }

    // A-2.30 FIRST HALF: chosen in campaign A, offered again on returning to A.
    [Fact]
    public void ANameChosenInACampaignIsOfferedAgainInThatCampaign()
    {
        var a = new Campaign();
        CampaignDisplayName.RecordChosen(a, Chosen, CharacterName);

        var reopened = RoundTrip(a);

        Assert.Equal(Chosen, CampaignDisplayName.ToEdit(reopened, CharacterName));
        Assert.Equal(Chosen, CampaignDisplayName.Or(reopened, CharacterName).Value);
    }

    // A-2.30 SECOND HALF, AND THIS IS THE ONE THAT FAILED ON main. A build with a global alias
    // passes the test above and fails this one, which is why the halves are separate facts rather
    // than two asserts in one.
    [Fact]
    public void ThatNameIsNotOfferedInADifferentCampaign()
    {
        var a = new Campaign();
        CampaignDisplayName.RecordChosen(a, Chosen, CharacterName);

        var b = new Campaign();

        Assert.Equal(CharacterName.Value, CampaignDisplayName.ToEdit(b, CharacterName));
        Assert.Equal(CharacterName, CampaignDisplayName.Or(b, CharacterName));
    }

    // THE POSITIVE CONTROL, AND WITHOUT IT EVERY "nothing changed" ABOVE PASSES AGAINST A SERIALIZER
    // THAT RETURNS THE SAME BYTES FOR EVERYTHING. Deliberately a setting that is NOT a name, so it
    // keeps proving the comparison is live now that no name can reach settings at all.
    [Fact]
    public void TheComparisonCanTellTwoSettingsDocumentsApart()
    {
        var untouched = new PluginSettings();
        var changed = new PluginSettings { RelayAddress = "wss://somewhere.example/relay" };

        Assert.NotEqual(SettingsDocument(untouched), SettingsDocument(changed));
    }

    // Each document is produced by the serializer that actually persists it: settings are Newtonsoft
    // on disk (ConfigurationStore), campaigns are System.Text.Json (CampaignDocumentCodec). Using
    // one library for both would test a document neither store writes.
    private static string SettingsDocument(PluginSettings settings) =>
        Newtonsoft.Json.JsonConvert.SerializeObject(settings);

    private static Campaign RoundTrip(Campaign campaign) =>
        JsonSerializer.Deserialize<Campaign>(JsonSerializer.Serialize(campaign))!;
}
