using DungeonMasterXIV.Services;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class CommandDispatcherTests
{
    [Theory]
    [InlineData("settings")]
    [InlineData("Settings")]
    [InlineData("SETTINGS")]
    [InlineData("  settings  ")]
    public void SettingsArgumentOpensTheSettingsWindow(string arguments)
    {
        var (dispatcher, mainToggles, settingsOpens) = Build();

        dispatcher.Execute(arguments);

        Assert.Equal(0, mainToggles.Count);
        Assert.Equal(1, settingsOpens.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("wat")]
    [InlineData("settings please")]
    public void EveryOtherArgumentTogglesTheMainWindow(string arguments)
    {
        var (dispatcher, mainToggles, settingsOpens) = Build();

        dispatcher.Execute(arguments);

        Assert.Equal(1, mainToggles.Count);
        Assert.Equal(0, settingsOpens.Count);
    }

    private static (CommandDispatcher Dispatcher, Counter MainToggles, Counter SettingsOpens) Build()
    {
        var mainToggles = new Counter();
        var settingsOpens = new Counter();
        var dispatcher = new CommandDispatcher(mainToggles.Increment, settingsOpens.Increment);
        return (dispatcher, mainToggles, settingsOpens);
    }

    private sealed class Counter
    {
        public int Count { get; private set; }

        public void Increment() => Count++;
    }
}
