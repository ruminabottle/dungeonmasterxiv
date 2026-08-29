using System.Linq;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Newtonsoft.Json;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-146: saving and loading the settings does not change what is remembered.
/// </summary>
/// <remarks>
/// <para>
/// <b>The relink memory DOUBLED on every save/load — 1, 2, 4, 8, 16, 32, unbounded.</b>
/// <c>RelinkMemory</c> exposed the same list through two public gettable members, so Newtonsoft wrote
/// both into the settings document; on load it populates a read-only collection property by ADDING to
/// it, so every entry arrived a second time in the same list. Twenty ordinary open-the-settings cycles
/// reach about a million entries.
/// </para>
/// <para>
/// <b>THE ASSERTION IS THE COUNT AFTER A ROUND TRIP, NEVER THE SHAPE OF THE DOCUMENT.</b> qa-1's point
/// when filing it, and it is the difference between a test that holds and one that looks like it
/// does: a test asserting the JSON has no <c>"All"</c> key passes the moment the property is renamed
/// while still being serialised. The defect is not a key name, it is that a round trip is not an
/// identity.
/// </para>
/// <para>
/// Newtonsoft rather than System.Text.Json because Dalamud persists plugin config with Newtonsoft —
/// testing with the other serialiser would be measuring a round trip nobody performs.
/// </para>
/// </remarks>
public class RelinkMemorySurvivesARoundTripTests
{
    private const string Code = "BCDFGH";
    private const int Cycles = 5;

    // THE DEFECT. Fails if the memory is reachable through more than one serialised member: 1 entry
    // becomes 32 after five cycles.
    [Fact]
    public void RememberingOneParticipantStillRemembersOneAfterFiveSaveLoadCycles()
    {
        var settings = new PluginSettings();
        settings.Relink.Remember(SessionCode.FromValid(Code), System.Guid.NewGuid());

        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            settings = RoundTrip(settings);
        }

        Assert.Single(settings.Relink.Remembered);
    }

    // THE TIGHTEST FORM. One cycle is where the doubling starts, and naming it separately means a
    // failure says whether the identity broke at all or only compounded.
    [Fact]
    public void OneSaveAndLoadIsAnIdentity()
    {
        var settings = new PluginSettings();
        settings.Relink.Remember(SessionCode.FromValid(Code), System.Guid.NewGuid());

        Assert.Single(RoundTrip(settings).Relink.Remembered);
    }

    // AND THE ENTRY IS STILL THE ONE THAT WAS STORED. A count of one is also what you get from a
    // "fix" that drops the memory and re-adds an empty shell, or that persists nothing and starts
    // fresh -- both of which lose the relink and would pass every assertion above.
    [Fact]
    public void TheRememberedParticipantSurvivesTheRoundTripIntact()
    {
        var id = System.Guid.NewGuid();
        var settings = new PluginSettings();
        settings.Relink.Remember(SessionCode.FromValid(Code), id);

        var loaded = RoundTrip(RoundTrip(settings));

        Assert.Equal(id, loaded.Relink.IdFor(SessionCode.FromValid(Code)));
    }

    // THE PREMISE. Everything above asserts a count of one, which an empty list makes vacuously
    // wrong rather than vacuously right -- but only if Remember() actually stored something in the
    // first place. This says the fixture is real before the round trip is blamed for anything.
    [Fact]
    public void TheFixtureStoresSomethingBeforeAnyRoundTrip()
    {
        var settings = new PluginSettings();
        settings.Relink.Remember(SessionCode.FromValid(Code), System.Guid.NewGuid());

        Assert.Single(settings.Relink.Remembered);
    }

    private static PluginSettings RoundTrip(PluginSettings settings) =>
        JsonConvert.DeserializeObject<PluginSettings>(JsonConvert.SerializeObject(settings))!;
}
