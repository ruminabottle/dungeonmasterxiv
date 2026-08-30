using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.19's product surface: <b>a person running the plugin has a route by which to send a
/// message</b> (A-2.41).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE REQUIREMENT THIS PINS IS REACHABILITY, NOT BEHAVIOUR.</b> DMXENG-121 shipped the send
/// path and its refusal correctly, and every one of its tests still passed while <b>nothing outside
/// Core constructed a message</b> — nine types built, merged and green with no way for a player to
/// use them. So what is asserted here is the existence of a call FROM the window layer, which no
/// behavioural test of the message types can see.
/// </para>
/// <para>
/// <b>Source-reading, because no test here can execute this file.</b> The test project references
/// Core alone and may never reference the plugin, so <c>SessionWindow</c> and
/// <c>MessageComposeView</c> cannot be constructed. <c>BothRosterViewsRenderThroughOnePlaceTests</c>
/// is the precedent and states the limit this shares: <b>it asserts the SHAPE of the code, not that
/// the pixels are right.</b> A-2.41's in-game half is not discharged here.
/// </para>
/// <para>
/// <b>COMMENTS ARE STRIPPED BEFORE ANYTHING IS ASSERTED, AND THAT IS THE POINT RATHER THAN
/// TIDINESS.</b> <c>MessageComposeView</c>'s own documentation names <c>SessionMembership.Say</c>
/// in prose. A scan that read the raw file would go green on that sentence alone — <b>the wiring
/// could be deleted entirely and this test would still pass, which is the exact vacuous shape this
/// ticket exists to close.</b> The precedent's helper strips only <c>/* */</c> blocks; these files
/// document in <c>///</c> lines, so those are stripped too.
/// <see cref="TheScanIgnoresDocumentationAndWouldNotPassOnACommentAlone"/> proves the stripping
/// works rather than assuming it.
/// </para>
/// </remarks>
public class APersonCanActuallySendAMessageTests
{
    // ---- A-2.41: the call into the send path, from the window layer rather than from a test.

    [Fact]
    public void TheWindowLayerCallsTheSendPath()
    {
        var source = CodeOf("Windows", "MessageComposeView.cs");

        Assert.Contains("Membership.Say(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheComposeSurfaceIsReachedFromTheSessionWindow()
    {
        // A call nothing draws is a surface nobody can reach -- the same defect one layer up, and
        // the reason this assertion is separate from the one above.
        var source = CodeOf("Windows", "SessionWindow.cs");

        Assert.Contains("new MessageComposeView(", source, StringComparison.Ordinal);
        Assert.Contains("_compose.Draw()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSessionWindowIsRegisteredWithTheWindowSystem()
    {
        // And the window it is drawn from is itself registered. A window built and never added is
        // reachable by nothing, and no call-site assertion above would notice.
        var source = CodeOf("Plugin.cs");

        Assert.Contains("_windowSystem.AddWindow(_sessionWindow)", source, StringComparison.Ordinal);
    }

    // ---- A-2.35: the person who typed it is TOLD.

    [Fact]
    public void ARefusalIsShownRatherThanDroppedOnTheFloor()
    {
        // Say returns a draft that names its own fault. A surface that discarded it would fail
        // A-2.35 while the wire behaviour underneath stayed perfectly correct -- so the criterion
        // cannot be discharged anywhere but here.
        var source = CodeOf("Windows", "MessageComposeView.cs");

        Assert.Contains("draft.Reason", source, StringComparison.Ordinal);
        Assert.Contains("ImGui.TextUnformatted(refusal)", source, StringComparison.Ordinal);
    }

    // ---- The controls. Every assertion above rests on the scan reading real code.

    [Fact]
    public void TheScanActuallyReadsTheFilesItClaimsTo()
    {
        // An unreadable path returns empty, and empty satisfies every DoesNotContain and fails
        // every Contains for a reason that has nothing to do with the tree.
        Assert.Contains("class MessageComposeView", CodeOf("Windows", "MessageComposeView.cs"), StringComparison.Ordinal);
        Assert.Contains("class SessionWindow", CodeOf("Windows", "SessionWindow.cs"), StringComparison.Ordinal);
        Assert.Contains("class Plugin", CodeOf("Plugin.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheScanIgnoresDocumentationAndWouldNotPassOnACommentAlone()
    {
        // THE CONTROL FOR THE CONTROL. This sentence exists only in MessageComposeView's `///`
        // documentation. If it survives stripping, then so would the doc's mention of
        // SessionMembership.Say -- and TheWindowLayerCallsTheSendPath would pass on prose with the
        // wiring deleted.
        var stripped = CodeOf("Windows", "MessageComposeView.cs");
        var raw = File.ReadAllText(Path.Combine(RepositoryRoot(), "Windows", "MessageComposeView.cs"));

        Assert.Contains("THIS IS THE PRODUCT SURFACE", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("THIS IS THE PRODUCT SURFACE", stripped, StringComparison.Ordinal);
    }

    /// <summary>Reads a file with its comments removed, so prose can never satisfy an assertion.</summary>
    private static string CodeOf(params string[] parts)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));
        var withoutBlocks = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return Regex.Replace(withoutBlocks, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DungeonMasterXIV.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
