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
    public void EachWindowsRecordedStateIsIndependentOfTheOther()
    {
        var settings = new PluginSettings();

        settings.RecordMainWindowOpen(true);

        Assert.True(settings.MainWindowOpen);
        Assert.False(settings.SettingsWindowOpen);
    }
}
