using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-63: the build SDK is pinned by a <c>global.json</c> at the repository root.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS A DECLARED PROXY. It checks PRESENCE, not EFFECT.</b> It asserts that a
/// <c>global.json</c> exists and names an SDK version and a <c>rollForward</c> policy. It cannot tell
/// a binding pin from an inert one, and nothing written in this project could: these tests are
/// already running under whichever SDK resolved before any of them executed, so by the time this
/// method runs the question it would want to ask has been answered and discarded.
/// </para>
/// <para>
/// <b>The effect was verified by mutation instead</b>, and the record is in PR #98: pinning
/// <c>10.0.999</c>, which is not installed, made <c>dotnet build</c> fail at SDK resolution with
/// exit code <b>155</b>, naming both the requested version and the path of this file, before any
/// project was loaded. That is what proves the file is read. A successful build proves nothing here —
/// it succeeds identically with no <c>global.json</c> at all.
/// </para>
/// <para>
/// So what this test is actually for: <b>deletion and hollowing-out</b>. If someone removes the file,
/// empties the version, or drops <c>rollForward</c>, this reddens. That is a narrower job than the
/// name suggests, which is why the limit is written here rather than left for a reader to discover.
/// </para>
/// </remarks>
public class TheSdkIsPinnedTests
{
    [Fact]
    public void TheRepositoryPinsAnSdkVersion()
    {
        var pin = ThePin();

        Assert.True(pin.TryGetProperty("sdk", out var sdk), "global.json has no 'sdk' section.");
        Assert.True(sdk.TryGetProperty("version", out var version), "global.json pins no SDK version.");
        Assert.False(string.IsNullOrWhiteSpace(version.GetString()), "The pinned SDK version is empty.");
    }

    // rollForward is asserted because its ABSENCE is not a neutral default: omitted, the SDK rolls
    // forward to the latest patch on its own, which is the behaviour this pin exists to remove. A
    // pin without it reads as strict and is not.
    [Fact]
    public void ThePinStatesItsRollForwardPolicyRatherThanInheritingOne()
    {
        var sdk = ThePin().GetProperty("sdk");

        Assert.True(sdk.TryGetProperty("rollForward", out var policy), "global.json states no rollForward policy.");
        Assert.False(string.IsNullOrWhiteSpace(policy.GetString()), "The rollForward policy is empty.");
    }

    private static JsonElement ThePin()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "DungeonMasterXIV.sln")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        var path = Path.Combine(root!.FullName, "global.json");
        Assert.True(File.Exists(path), $"No global.json at the repository root ({path}). The SDK is unpinned.");

        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }
}
