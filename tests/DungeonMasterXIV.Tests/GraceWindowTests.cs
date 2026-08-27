using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class GraceWindowTests
{
    // R-1.4's whole shape: grace, then a clean end. Fails if: expiry stops happening, which leaves
    // clients holding stale state forever — the failure R-1.4 exists to prevent.
    [Fact]
    public void TheSessionEndsWhenTheWindowRunsOut()
    {
        var grace = new GraceWindow();
        grace.HostLost();

        Assert.True(grace.Tick(GraceWindow.Default));

        Assert.True(grace.HasExpired);
        Assert.False(grace.IsRunning);
    }

    // Fails if: the window ends early, kicking everyone out of a session whose host was reachable.
    [Fact]
    public void AHostThatReturnsInsideTheWindowKeepsTheSession()
    {
        var grace = new GraceWindow();
        grace.HostLost();
        grace.Tick(GraceWindow.Default - TimeSpan.FromSeconds(1));

        Assert.True(grace.HostReturned());

        Assert.False(grace.HasExpired);
        Assert.False(grace.IsRunning);
    }

    // Fails if: the countdown is not readable while it runs. R-1.4 requires clients to show plainly
    // that state is no longer live, which needs something to show.
    [Fact]
    public void TimeRemainingIsVisibleWhileTheWindowRuns()
    {
        var grace = new GraceWindow();
        grace.HostLost();

        grace.Tick(TimeSpan.FromSeconds(30));

        Assert.True(grace.IsRunning);
        Assert.Equal(GraceWindow.Default - TimeSpan.FromSeconds(30), grace.Remaining);
    }

    // Fails if: the clock runs while the host is present, which would end healthy sessions.
    [Fact]
    public void TimeDoesNotPassWhileTheHostIsPresent()
    {
        var grace = new GraceWindow();

        Assert.False(grace.Tick(TimeSpan.FromHours(3)));
        Assert.False(grace.HasExpired);
    }

    // Fails if: a second loss restarts a window that already expired, resurrecting a dead session.
    [Fact]
    public void AnExpiredWindowDoesNotRestartOnAnotherLoss()
    {
        var grace = new GraceWindow();
        grace.HostLost();
        grace.Tick(GraceWindow.Default);

        grace.HostLost();

        Assert.True(grace.HasExpired);
        Assert.False(grace.IsRunning);
    }

    // The cross-check C3 found the hard way: the grace window is the side that moves, so it is the
    // side that must refuse. Fails if: a window shorter than three keepalive intervals is accepted,
    // at which point an ordinary lull between rolls ends a live session mid-play.
    [Theory]
    [InlineData(89)]
    [InlineData(30)]
    public void AWindowTooShortForTheKeepaliveIsRefusedRatherThanClamped(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraceWindow(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void TheDefaultWindowIsAcceptedAndIsRule14sTwoMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), GraceWindow.Default);
        Assert.True(TransportContract.IsKeepAliveSafeFor(GraceWindow.Default));
    }
}

/// <summary>
/// The gap between R-1.4's grace window and the relay freeing a session code immediately.
/// </summary>
public class SupersededCodeTests
{
    private static readonly SessionCode Original = SessionCode.FromValid("BKD7RM");
    private static readonly SessionCode Replacement = SessionCode.FromValid("CFGH23");

    // Fails if: a code taken during the grace window is swapped in silently. The DM would carry on
    // hosting under a new code while every player holds the old one — nothing errors, nothing looks
    // wrong, and the session simply cannot be joined. Silence is the defect.
    [Fact]
    public void ACodeTakenDuringGraceIsSurfacedRatherThanSwappedInSilently()
    {
        var host = new HostSession();
        host.Start(Original);
        host.Registered();

        host.CodeSuperseded(Replacement);

        Assert.True(host.CodeChangedMidSession);
        Assert.Equal(Original, host.SupersededCode);
        Assert.Equal(Replacement, host.Code);
    }

    // Fails if: the warning cannot be dismissed, which would nag a DM who has already read the new
    // code out — and a warning that never goes away is one people learn to ignore.
    [Fact]
    public void TheWarningClearsOnceTheDmHasToldTheirPlayers()
    {
        var host = new HostSession();
        host.Start(Original);
        host.CodeSuperseded(Replacement);

        host.AcknowledgeCodeChange();

        Assert.False(host.CodeChangedMidSession);
        Assert.Equal(Replacement, host.Code);
    }

    // Fails if: an ordinary session claims its code changed.
    [Fact]
    public void AnUndisturbedSessionReportsNoCodeChange()
    {
        var host = new HostSession();
        host.Start(Original);
        host.Registered();

        Assert.False(host.CodeChangedMidSession);
    }

    // Fails if: ending a session leaves the warning behind for the next one.
    [Fact]
    public void EndingTheSessionClearsTheSupersededCode()
    {
        var host = new HostSession();
        host.Start(Original);
        host.CodeSuperseded(Replacement);

        host.Stop();

        Assert.False(host.CodeChangedMidSession);
    }
}

/// <summary>
/// Obligation 1: the fingerprint length and the admission expiry are one decision in two files.
/// </summary>
public class FingerprintExpiryCouplingTests
{
    // THE CROSS-GUARD, C5's half. Its counterpart is
    // tests/DungeonMasterXIV.Tests/AdmissionDeadlineTests.cs, which pins the fifteen minutes, and
    // tests/DungeonMasterXIV.Tests/KeyFingerprintTests.cs, which pins the eleven characters.
    //
    // Fails if: the admission prompt's expiry is removed or its window changed without the
    // fingerprint length moving with it. R-1.3a decided eleven characters ONLY because the prompt
    // expires — against a bounded window a ten-month second-preimage search is hopeless rather than
    // merely expensive. Remove the expiry and eleven must become fourteen.
    //
    // A comment does not discharge this. A decision recorded rather than applied is what stranded
    // R-1.3a in the first place and produced C8.
    [Fact]
    public void ElevenCharactersHoldsOnlyBecauseTheAdmissionPromptExpires()
    {
        Assert.Equal(11, KeyFingerprint.Characters);
        Assert.Equal(TimeSpan.FromMinutes(15), AdmissionDeadline.Window);
    }

    // The expiry is not merely declared — it is reachable and it fires. A window that exists as a
    // constant nobody enforces is the same decision-recorded-not-applied failure one layer along.
    [Fact]
    public void TheExpiryActuallyRemovesARequestRatherThanOnlyBeingDeclared()
    {
        var now = new DateTimeOffset(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);
        var desk = new AdmissionDesk();
        desk.Receive(new PendingAdmission("PEER-1", "BKD-7RM-CDF-GH", AdmissionDeadline.DecidedByHost(now)));

        Assert.Single(desk.ExpireLapsed(now.Add(AdmissionDeadline.Window)));
        Assert.Empty(desk.Pending);
    }
}
