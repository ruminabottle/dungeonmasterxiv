using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Clause 2 of the transport contract. The values are reasoned rather than measured; the
/// relationships between them are not, and these are the relationships.
/// </summary>
public class TransportContractTests
{
    private static readonly TimeSpan DefaultGraceWindow = TimeSpan.FromMinutes(2);

    // Fails if: the interval is raised above the grace window, at which point an ordinary lull
    // between rolls trips host-loss detection and ends a live session mid-play. This is the hard
    // bound R-1.4 implies, asserted rather than left to a comment.
    [Fact]
    public void TheKeepAliveIntervalIsSafeAgainstTheDefaultGraceWindow()
    {
        Assert.True(TransportContract.IsKeepAliveSafeFor(DefaultGraceWindow));
    }

    // The edit that actually endangers this. R-1.4's grace window is settable on purpose, so the
    // dangerous change is someone lowering it rather than raising the interval. Fails if: the bound
    // stops being checkable, which is what would let a short grace window through.
    [Theory]
    [InlineData(89)]
    [InlineData(30)]
    [InlineData(1)]
    public void AGraceWindowTooShortForTheKeepAliveIsRejected(int graceSeconds)
    {
        Assert.False(TransportContract.IsKeepAliveSafeFor(TimeSpan.FromSeconds(graceSeconds)));
    }

    // Fails if: the margin is quietly reduced to one interval, which would mean a single lost ping
    // could reach host-loss detection.
    [Fact]
    public void TheBoundRequiresRoomForMoreThanOneLostPing()
    {
        Assert.True(TransportContract.RequiredGraceMargin >= 3);
        Assert.True(TransportContract.IsKeepAliveSafeFor(
            TransportContract.KeepAliveInterval * TransportContract.RequiredGraceMargin));
        Assert.False(TransportContract.IsKeepAliveSafeFor(
            TransportContract.KeepAliveInterval * TransportContract.RequiredGraceMargin - TimeSpan.FromSeconds(1)));
    }

    // Fails if: the timeout drops to or below the interval, which would make a single missed pong a
    // disconnection and turn ordinary jitter into a dropped session.
    [Fact]
    public void APongMayBeMissedWithoutDroppingTheConnection()
    {
        Assert.True(TransportContract.KeepAliveTimeout > TransportContract.KeepAliveInterval);
    }
}
