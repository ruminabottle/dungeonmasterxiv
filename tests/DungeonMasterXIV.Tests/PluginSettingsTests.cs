using DungeonMasterXIV.Data;
using Newtonsoft.Json;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class PluginSettingsTests
{
    [Fact]
    public void FreshSettingsStartWithBothWindowsClosedAndRestoreOn()
    {
        var settings = new PluginSettings();

        Assert.False(settings.MainWindowOpen);
        Assert.False(settings.SettingsWindowOpen);
        Assert.True(settings.RestoreWindowState);
    }

    [Fact]
    public void RoundTripPreservesEveryPersistedField()
    {
        var saved = new PluginSettings
        {
            MainWindowOpen = true,
            SettingsWindowOpen = true,
            RestoreWindowState = false,
        };

        var loaded = JsonConvert.DeserializeObject<PluginSettings>(JsonConvert.SerializeObject(saved));

        Assert.NotNull(loaded);
        Assert.True(loaded!.MainWindowOpen);
        Assert.True(loaded.SettingsWindowOpen);
        Assert.False(loaded.RestoreWindowState);
    }

    [Fact]
    public void SettingsWrittenByAnOlderBuildKeepTheirDefaultsForFieldsThatDidNotExistYet()
    {
        // Dalamud hands us whatever is on disk, which may predate a field. A missing field must
        // come back as its default rather than as false, or "restore my windows" silently flips
        // off for every existing user the first time we add a setting.
        var loaded = JsonConvert.DeserializeObject<PluginSettings>("{\"MainWindowOpen\":true}");

        Assert.NotNull(loaded);
        Assert.True(loaded!.MainWindowOpen);
        Assert.True(loaded.RestoreWindowState);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldOpenOnLoadRequiresBothRestoreAndTheRememberedState(
        bool restoreWindowState,
        bool wasOpen,
        bool expected)
    {
        var settings = new PluginSettings { RestoreWindowState = restoreWindowState };

        Assert.Equal(expected, settings.ShouldOpenOnLoad(wasOpen));
    }

    [Fact]
    public void RecordingAChangedMainWindowStateReportsTheChangeAndStoresIt()
    {
        var settings = new PluginSettings { MainWindowOpen = false };

        Assert.True(settings.RecordMainWindowOpen(true));
        Assert.True(settings.MainWindowOpen);
    }

    [Fact]
    public void RecordingTheMainWindowStateItAlreadyHasReportsNoChange()
    {
        // The caller saves only when this returns true. Returning true here would rewrite an
        // identical config file every time the plugin loads with the window already open.
        var settings = new PluginSettings { MainWindowOpen = true };

        Assert.False(settings.RecordMainWindowOpen(true));
        Assert.True(settings.MainWindowOpen);
    }

    [Fact]
    public void RecordingAChangedSettingsWindowStateReportsTheChangeAndStoresIt()
    {
        var settings = new PluginSettings { SettingsWindowOpen = true };

        Assert.True(settings.RecordSettingsWindowOpen(false));
        Assert.False(settings.SettingsWindowOpen);
    }

    [Fact]
    public void RecordingTheSettingsWindowStateItAlreadyHasReportsNoChange()
    {
        var settings = new PluginSettings { SettingsWindowOpen = false };

        Assert.False(settings.RecordSettingsWindowOpen(false));
        Assert.False(settings.SettingsWindowOpen);
    }

    [Fact]
    public void AFirstRunWithNothingOnDiskRequiresAWriteOnLoad()
    {
        // BUG-1. The load path never wrote, so a user who loaded the plugin and opened no window
        // had no config file at all, and therefore no schema version on disk. R-0.5 wants the
        // version there from the first load rather than from the first click.
        Assert.True(PluginSettings.RequiresWriteOnLoad(null));
    }

    [Fact]
    public void ConfigAlreadyAtTheCurrentSchemaVersionRequiresNoWriteOnLoad()
    {
        // This is the case that separates the fix from the careless version of it. Calling Save()
        // unconditionally on load also satisfies A-0.5, and rewrites an identical file every time
        // the plugin loads -- which is the exact cost RecordMainWindowOpen exists to avoid.
        Assert.False(PluginSettings.RequiresWriteOnLoad(PluginSettings.CurrentSchemaVersion));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ConfigAtSomeOtherSchemaVersionIsLeftAloneOnLoad(int versionOnDisk)
    {
        // A version we do not recognise is a migration's problem, and there is no migration yet.
        // Stamping it down to the current version would overwrite a config a newer build wrote.
        Assert.False(PluginSettings.RequiresWriteOnLoad(versionOnDisk));
    }

    [Fact]
    public void EachWindowsRecordedStateIsIndependentOfTheOther()
    {
        var settings = new PluginSettings();

        settings.RecordMainWindowOpen(true);

        Assert.True(settings.MainWindowOpen);
        Assert.False(settings.SettingsWindowOpen);
    }
}
