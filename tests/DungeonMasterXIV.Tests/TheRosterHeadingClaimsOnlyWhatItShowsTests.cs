using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The roster heading is a claim about what is below it, and it must be true (R-1.3f).
/// </summary>
/// <remarks>
/// <para>
/// <b>Value tests, because the previous version of this guard was defeated in one line.</b> It read
/// the window's source and asserted the CONSTANT's text. The Code Reviewer left the constant honest
/// and passed a literal to the draw call instead — every test green, and a user shown a heading
/// claiming to show everyone. It guarded the value while nothing guarded that the value was USED:
/// the same family as a check that reads only the first occurrence.
/// </para>
/// <para>
/// With the heading in Core there is nothing to bypass here, and
/// <c>BothRosterViewsRenderThroughOnePlaceTests</c> holds the other half — that the window renders
/// this value rather than a literal of its own.
/// </para>
/// </remarks>
public class TheRosterHeadingClaimsOnlyWhatItShowsTests
{
    private const string RelaxWithDmxeng33 =
        "RELAX this as part of DMXENG-33, once the roster includes the host — do not delete it. "
        + "Until then the roster structurally omits the DM, so a heading claiming to show everyone "
        + "tells a player the DM is not here, which is false rather than incomplete.";

    [Theory]
    [InlineData("everyone")]
    [InlineData("everybody")]
    [InlineData("all participants")]
    [InlineData("who is here")]
    [InlineData("in the session")]
    public void ItDoesNotClaimToShowEveryone(string overclaim) =>
        Assert.False(
            RosterHeading.Text.Contains(overclaim, StringComparison.OrdinalIgnoreCase),
            $"The roster heading claims \"{overclaim}\", and it cannot. {RelaxWithDmxeng33}");

    // The control. Every refusal above is satisfied by an empty heading, which would render a
    // nameless list rather than an overclaiming one -- a different defect, not an absence of one.
    [Fact]
    public void ItStillNamesWhatItShows()
    {
        Assert.False(string.IsNullOrWhiteSpace(RosterHeading.Text));
        Assert.Contains("Players", RosterHeading.Text, StringComparison.Ordinal);
    }
}
