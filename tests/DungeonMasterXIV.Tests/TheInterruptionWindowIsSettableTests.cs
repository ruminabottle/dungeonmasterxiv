using System;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.4's window is a settable value rather than a magic number (A-1.23).
/// </summary>
/// <remarks>
/// <para>
/// <b>What this file does NOT yet prove, stated first so a green run is not over-read.</b> A-1.23 is
/// <i>"no literal in the grace path"</i>, and the path from this setting to
/// <see cref="GraceWindow"/> runs through <c>SessionCoordinator</c> and <c>SessionInterruption</c>,
/// both of which are held by DMXENG-12. <b>Until that threading lands, this setting exists and
/// nothing reads it</b> — so these tests assert the value and its refusals, and they do NOT assert
/// that the window in a running session comes from here. Do not read them as discharging A-1.23.
/// </para>
/// <para>
/// <b>And A-1.27 cannot be written at all yet</b> — <i>"both windows read one settable value"</i>
/// needs two windows, and the second comes into existence in DMXENG-12. A test shaped
/// <i>"every window reads the setting"</i> over a set of one passes vacuously, which is the exact
/// failure BUG-55 exists to prevent. It arrives with the threading, derived over the windows the
/// coordinator exposes so a third unwired one breaks it.
/// </para>
/// </remarks>
public sealed class TheInterruptionWindowIsSettableTests
{
    // Fails if: the setting ships with no value, or with one that is not R-1.4's. BUG-54 is why this
    // is pinned rather than assumed -- the number was wrong once and a correct fix moved the literal
    // without making it single-sourced.
    [Fact]
    public void TheDefaultIsRule14sFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), new PluginSettings().InterruptionWindow);
    }

    // The reason this setting is not just a number on a class. Fails if: a value short enough to end
    // live sessions mid-play is accepted -- which is the dangerous edit TransportContract names,
    // because it is the user-facing direction.
    // The floor is MEASURED, not assumed: KeepAliveInterval is 30s and RequiredGraceMargin is 3, so
    // the floor is 90 SECONDS. An earlier version of this file guessed one minute was acceptable and
    // guessed that 29s was "just under" the floor -- both wrong, and the second would have shipped a
    // misleading label even once the row went green.
    [Theory]
    [InlineData(0, "zero")]
    [InlineData(1, "one second")]
    [InlineData(60, "one minute -- BELOW the 90s floor, which is not obvious and is why it is here")]
    [InlineData(89, "one second under the floor")]
    public void AWindowTooShortForTheKeepaliveIsRefused(int seconds, string why)
    {
        var settings = new PluginSettings();

        Assert.False(settings.RecordInterruptionWindow(TimeSpan.FromSeconds(seconds)), why);
        Assert.Equal(GraceWindow.Default, settings.InterruptionWindow);
    }

    // The other half of the probe. Without it "refuse everything" would pass every case above.
    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(60)]
    public void AWindowLongEnoughIsAccepted(int minutes)
    {
        var settings = new PluginSettings();
        var requested = TimeSpan.FromMinutes(minutes);

        Assert.Equal(requested != GraceWindow.Default, settings.RecordInterruptionWindow(requested));
        Assert.Equal(requested, settings.InterruptionWindow);
    }

    // Fails if: an unchanged value reports as a change, which would rewrite the config file on every
    // keystroke that changes nothing -- the same reason RecordDisplayNameAlias reports it.
    [Fact]
    public void RecordingTheSameWindowReportsNoChange()
    {
        var settings = new PluginSettings();

        Assert.False(settings.RecordInterruptionWindow(GraceWindow.Default));
    }

    // THE DISK PATH, which is the one that matters and the one Record cannot guard. The setter is
    // public because the serialiser needs it, so a hand-edited file reaches the property directly
    // and never passes through the refusal above.
    //
    // Fails if: a corrupt or hand-shortened config is trusted. That would end live sessions mid-play
    // for no visible reason, which is precisely what the refusal exists to stop -- and going through
    // the front door would make the back door the only way in.
    [Fact]
    public void AnUnsafeValueFromDiskFallsBackRatherThanBeingTrusted()
    {
        var settings = new PluginSettings { InterruptionWindow = TimeSpan.FromSeconds(1) };

        Assert.Equal(GraceWindow.Default, settings.InterruptionWindowOrDefault());
    }

    // And it does not fall back on a value that is merely unusual. Fails if: the guard is really an
    // equality check against the default wearing a validator's clothes.
    [Fact]
    public void ASafeValueFromDiskIsUsedEvenWhenItIsNotTheDefault()
    {
        var unusual = TimeSpan.FromMinutes(37);
        var settings = new PluginSettings { InterruptionWindow = unusual };

        Assert.Equal(unusual, settings.InterruptionWindowOrDefault());
    }

    // The floor is TransportContract's, not a second copy of it. Fails if: someone restates the
    // margin here or in PluginSettings and the two drift -- the defect BUG-55 is about, one layer up.
    [Fact]
    public void TheFloorIsTheTransportContractsRatherThanARestatedNumber()
    {
        var floor = TransportContract.KeepAliveInterval * TransportContract.RequiredGraceMargin;
        var settings = new PluginSettings();

        Assert.True(settings.RecordInterruptionWindow(floor), "the floor itself must be acceptable");

        var justUnder = floor - TimeSpan.FromTicks(1);
        Assert.False(settings.RecordInterruptionWindow(justUnder), "a tick under the floor must not be");
    }
}
