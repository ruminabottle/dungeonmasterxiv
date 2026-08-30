using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The wiring itself, guarded — <b>because DMXENG-103 failed QA once for landing the mechanism
/// fully tested and never connected to anything.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS THE GAP THAT LET IT HAPPEN.</b> Every retention and deletion type had thorough unit
/// tests, and all of them passed, because a unit test constructs its own subject — <c>new
/// RetainedLogStore(...)</c> in a fixture proves the store works and says <i>nothing</i> about
/// whether the shipped build ever builds one. The whole mechanism can be green while
/// <c>new RetainedLogFileArchive</c> has zero call sites in the product. It did.
/// </para>
/// <para>
/// <b>IT IS A TEXTUAL PROXY AND THE LIMIT IS STATED RATHER THAN LEFT TO A GREEN RUN.</b> No test
/// project links the plugin, so nothing here can execute <c>Plugin</c>'s constructor or press the
/// delete button. What this can say is that the composition root NAMES these types and hands them to
/// the control; what it cannot say is that the control works in-game. That is the same limit
/// <c>TheNameFieldSaysWhenItIsFullTests</c> records for its own scan, and the same remedy.
/// </para>
/// <para>
/// <b>Comments are stripped before matching</b>, because this file's subjects are commented in prose
/// that names the very identifiers being asserted — a scan that counted those would pass against a
/// build where the wiring is only described.
/// </para>
/// </remarks>
public class TheRetainedLogWiringIsPresentTests
{
    // The composition root must BUILD the retained-log side and HAND it to the existing control.
    [Theory]
    [InlineData("new RetainedLogFileArchive")]
    [InlineData("new RetainedLogStore")]
    [InlineData("new CampaignDeletion")]
    [InlineData("new CampaignListWindow")]
    public void ThePluginConstructsTheRetainedLogSideAndHandsItToTheControl(string construction)
    {
        Assert.Contains(construction, CodeOf(Path.Combine(RepositoryRoot(), "Plugin.cs")), System.StringComparison.Ordinal);
    }

    // And the control's campaign arm must go THROUGH the composed deletion. Asserting only that
    // Plugin.cs builds a CampaignDeletion would pass a window that ignored it.
    [Fact]
    public void TheDeleteControlsCampaignArmGoesThroughTheComposedDeletion()
    {
        var source = CodeOf(Path.Combine(RepositoryRoot(), "Windows", "CampaignListWindow.cs"));

        Assert.Contains("deletion.Delete(id)", source, System.StringComparison.Ordinal);

        // The negative half: the arm must no longer bypass the composition. A window still calling
        // the campaign store directly would delete the campaign and orphan its log, which is the
        // defect this wiring exists to close.
        Assert.DoesNotContain("new DeletionPrompt(id => _store.Delete(id)", source, System.StringComparison.Ordinal);
    }

    // THE GUARD ON THE GUARD (BUG-48's shape): a scan over a path that does not resolve matches
    // nothing and goes green, so the read is asserted rather than assumed.
    [Fact]
    public void TheScanActuallyReadsTheFilesItClaimsTo()
    {
        Assert.Contains("class Plugin", CodeOf(Path.Combine(RepositoryRoot(), "Plugin.cs")), System.StringComparison.Ordinal);
        Assert.Contains(
            "class CampaignListWindow",
            CodeOf(Path.Combine(RepositoryRoot(), "Windows", "CampaignListWindow.cs")),
            System.StringComparison.Ordinal);
    }

    private static string CodeOf(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{path} is not where the scan looks, so it would pass over nothing.", path);
        }

        return Regex.Replace(File.ReadAllText(path), @"//[^\n]*", string.Empty);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DungeonMasterXIV.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root not found, so every scan below would be vacuous.");
    }
}
