using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.31a — the instrument for A-2.31 inspects WHAT IS PERSISTED: exactly one globally-stored
/// name-shaped value, the SQ-87 carry-over, and no other.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS ROW EXISTS BECAUSE A-2.31 HAD NO INSTRUMENT THAT COULD CATCH ITS OWN SUBJECT.</b>
/// <c>ADisplayNameIsNeverPersistedOutsideACampaignTests</c> compares two freshly-constructed
/// <see cref="PluginSettings"/> instances, which proves that CHOOSING a name touches nothing global
/// — a true and useful fact, and not the criterion. <b>A default-valued field is identical on both
/// sides of every one of those comparisons</b>, so that file would have stayed green through exactly
/// the change this ticket makes. A criterion about what is PERSISTED, checked by comparing objects
/// that were never persisted.
/// </para>
/// <para>
/// <b>IT FAILS IN BOTH DIRECTIONS, WHICH IS THE HALF THAT IS EASY TO LEAVE OUT.</b> A second
/// globally-stored name fails, and <b>NONE also fails</b> — without that, deleting the carry-over
/// would satisfy A-2.31 perfectly and silently retire the ruling that required it.
/// </para>
/// <para>
/// <b>TWO INDEPENDENT CHECKS, BECAUSE EITHER ALONE IS EVADABLE.</b> Counting name-shaped KEYS is a
/// control that fires only on the spellings its author picked — the objection the older file
/// correctly raises against itself. Counting occurrences of the VALUE catches a copy stored under any
/// key whatsoever, but cannot see a second name field holding something else. Together they cover
/// both, and the residual is stated rather than left implied: <b>a globally stored name that is
/// neither this value nor spelt with "name" or "alias" would pass.</b>
/// </para>
/// <para>
/// <b>What is serialised here is <see cref="PluginSettings"/> rather than the whole config file</b>,
/// because <c>Configuration</c> lives in the plugin project and no test project links it. The wrapper
/// adds one integer version and no name.
/// </para>
/// </remarks>
public class TheSettingsDocumentCarriesExactlyOneNameTests
{
    private const string CarriedOver = "The Cartographer";

    private static readonly DisplayName CharacterName = DisplayName.OrNone("Y'shtola Rhul");

    /// <summary>
    /// The persisted document, produced by the serializer that actually writes it. Dalamud persists
    /// settings with Newtonsoft, so anything else would measure a document nothing writes.
    /// </summary>
    private static string Persisted(PluginSettings settings) => JsonConvert.SerializeObject(settings);

    /// <summary>Every leaf member of a document, by name, however deeply nested.</summary>
    private static IReadOnlyList<string> LeafNames(string document) =>
        ((JContainer)JToken.Parse(document))
            .Descendants()
            .OfType<JProperty>()
            .Where(property => property.Value is not (JObject or JArray))
            .Select(property => property.Name)
            .ToList();

    /// <summary>
    /// Whether a member's name announces it holds a person's name. <b>Deliberately generous</b> — it
    /// is a net, and a net that is too tight is the failure the older instrument had.
    /// </summary>
    private static bool IsNameShaped(string member) =>
        member.Contains("name", StringComparison.OrdinalIgnoreCase)
        || member.Contains("alias", StringComparison.OrdinalIgnoreCase);

    // A-2.31a, THE CRITERION.
    [Fact]
    public void ThePersistedDocumentCarriesExactlyOneNameShapedMember()
    {
        var settings = new PluginSettings { DisplayNameAlias = CarriedOver };

        var nameShaped = LeafNames(Persisted(settings)).Where(IsNameShaped).ToList();

        Assert.Single(nameShaped);
    }

    // THE OTHER DIRECTION, AND THE ONE THAT IS EASY TO OMIT. A build that simply dropped the
    // carry-over would satisfy every "no name outside a campaign" assertion perfectly.
    [Fact]
    public void ABuildCarryingNoNameAtAllFailsToo()
    {
        var settings = new PluginSettings { DisplayNameAlias = CarriedOver };

        var nameShaped = LeafNames(Persisted(settings)).Where(IsNameShaped).ToList();

        Assert.NotEmpty(nameShaped);
        Assert.Contains(CarriedOver, Persisted(settings), StringComparison.Ordinal);
    }

    // THE VALUE CHECK, WHICH NO SPELLING GETS PAST. A second copy under any key at all shows up here
    // even though the key-name net above would miss it.
    [Fact]
    public void TheCarriedOverNameAppearsInThePersistedDocumentExactlyOnce()
    {
        var settings = new PluginSettings { DisplayNameAlias = CarriedOver };

        var document = Persisted(settings);
        var occurrences = document.Split(CarriedOver).Length - 1;

        Assert.Equal(1, occurrences);
    }

    // CHOOSING A NAME STILL CHANGES NOTHING GLOBALLY. A-2.31's original subject, re-asserted against
    // a settings object that now legitimately holds one -- so "nothing changed" is a real comparison
    // rather than one between two empty things.
    [Fact]
    public void AcceptingANameInACampaignLeavesThePersistedSettingsUntouched()
    {
        var settings = new PluginSettings { DisplayNameAlias = CarriedOver };
        var before = Persisted(settings);

        var campaign = new Campaign();
        CampaignDisplayName.RecordChosen(campaign, "Renn of the Ninth", CharacterName);

        Assert.Equal(before, Persisted(settings));
        Assert.DoesNotContain("Renn of the Ninth", Persisted(settings), StringComparison.Ordinal);
    }

    // ---- the instrument's own controls. Without these, every count above could be reporting on a
    // walker that finds nothing and a comparison that cannot fail.

    [Fact]
    public void TheWalkerActuallyReachesTheMembersItIsCounting()
    {
        var names = LeafNames(Persisted(new PluginSettings()));

        // Not a fixed number: the point is that the walk is live, not that settings has a given shape.
        Assert.Contains("RelayAddress", names);
        Assert.True(names.Count > 3, "The walker found almost nothing, so every count above is vacuous.");
    }

    [Fact]
    public void TheInstrumentRejectsASecondNameShapedMember()
    {
        // A DELIBERATELY FORBIDDEN DOCUMENT. If this passes the net, the criterion has no instrument.
        var forbidden = """{"DisplayNameAlias":"The Cartographer","RememberedPlayerName":"Renn"}""";

        Assert.Equal(2, LeafNames(forbidden).Count(IsNameShaped));
    }

    [Fact]
    public void TheInstrumentRejectsANameNestedDeeperRatherThanMissingIt()
    {
        // The obvious way to smuggle a second name past a shallow check.
        var forbidden = """{"DisplayNameAlias":"The Cartographer","Relink":{"PreferredName":"Renn"}}""";

        Assert.Equal(2, LeafNames(forbidden).Count(IsNameShaped));
    }
}
