using System;
using DungeonMasterXIV.Sizes;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// Every run says how much of what it looked at got a number.
/// </summary>
/// <remarks>
/// <b>The obligation the Deployment Manager added after this tool shipped:</b> a refusing tool must
/// report the refusal COUNT alongside its results, every run. A refusal is safe about the number and
/// unsafe about the census — it lies by omission about what it looked at, and a list of results
/// reads as a clean sweep to anyone not counting twice.
/// </remarks>
public class TheCensusSaysWhatWasNotMeasuredTests
{
    // THE ONE THAT MATTERS. If the line only appeared when something was refused, its absence would
    // be ambiguous between "nothing was refused" and "this build does not report it" -- the same
    // reassuring-direction failure it exists to close.
    [Fact]
    public void ItReportsEvenWhenNothingWasRefused()
    {
        var line = Census.Describe(measured: 12, refused: 0, files: 3);

        Assert.Contains("0 refused", line);
        Assert.Contains("12 measured", line);
    }

    [Fact]
    public void ARefusalIsCountedAndTheCoverageIsSpeltOut()
    {
        var line = Census.Describe(measured: 68, refused: 34, files: 81);

        Assert.Contains("34 NOT MEASURED", line);
        Assert.Contains("cover", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("68 of 102", line);
    }

    // The total must be measured PLUS refused, not the measured count wearing a total's name -- that
    // is precisely the misreading the census exists to prevent.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(0, 5)]
    [InlineData(68, 34)]
    public void TheTotalCountsWhatWasLookedAtRatherThanWhatWasAnswered(int measured, int refused) =>
        Assert.Contains($"{measured + refused} type(s)", Census.Describe(measured, refused, files: 1));
}
