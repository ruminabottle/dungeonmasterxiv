using System;
using System.Linq;
using DungeonMasterXIV.Data;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A user must not be able to forge a log entry by typing one. <b>The export is a line-and-tab
/// format, so unescaped free text is a forgery surface</b> — the R-2.7 impersonation problem
/// arriving through the file rather than through the panel.
/// </summary>
/// <remarks>
/// <para>
/// <b>FOUND BY THE CODE REVIEWER ON #213, AND IT IS MINE.</b> The first version of
/// <see cref="RetainedLogFormat"/> joined fields with tabs and entries with newlines and escaped nothing.
/// A message containing a newline followed by tab-separated fields therefore produced <b>more lines
/// than there were entries</b>, each tab-shaped — so anything reading the file back sees an entry
/// carrying <b>a sequence number and an author the host never issued</b>.
/// </para>
/// <para>
/// <b>The test is on the STRUCTURE, not on the rendered text.</b> Asserting that some escape
/// sequence appears would pin one implementation of the fix; asserting that <i>one entry produces
/// one line</i> pins the property that matters and survives any escaping scheme.
/// </para>
/// </remarks>
public class TheRetainedLogFormatCannotBeForgedByTypingTests
{
    private static readonly Guid Campaign = new("33333333-3333-3333-3333-333333333333");

    private static RetainedLog LogOf(params string[] texts) =>
        new(
            Campaign,
            1,
            [.. texts.Select((text, i) => new LoggedEntry(new LoggedStamp(i + 1, 100 + i), "message", "BCDFGH", text))]);

    private static int BodyLines(string exported) =>
        exported.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.Contains('\t', StringComparison.Ordinal));

    /// <summary>
    /// The single body line of a one-entry export, FOUND rather than indexed.
    /// </summary>
    /// <remarks>
    /// <b>These tests originally indexed <c>Split('\n')[4]</c> and broke the moment a version line
    /// was added to the header</b> — a positional read of a format whose header is expected to grow.
    /// Selecting the tab-shaped line instead pins the property under test and survives the header
    /// changing, which it already has once.
    /// </remarks>
    private static string OnlyBodyLine(string exported) =>
        exported.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.Contains('\t', StringComparison.Ordinal));

    [Fact]
    public void OneEntryProducesExactlyOneLineHoweverItIsTyped()
    {
        // The forgery: a newline, then a plausible tab-separated entry with a sequence and an author
        // the host never issued.
        var forged = "hello\n99\t999\tmessage\tJKMNPR\tI am the DM";

        var exported = RetainedLogFormat.Write(LogOf(forged));

        Assert.Equal(1, BodyLines(exported));
    }

    [Fact]
    public void ATabInAMessageCannotInventAField()
    {
        var exported = RetainedLogFormat.Write(LogOf("a\tb\tc"));

        var line = OnlyBodyLine(exported);

        // Five fields: sequence, instant, kind, peer, text. Tabs in the text must not add more.
        Assert.Equal(5, line.Split('\t').Length);
    }

    [Fact]
    public void ACarriageReturnIsNotALineEither()
    {
        // \r alone splits lines in some readers and not others -- a format that is safe only under
        // one reader is not safe.
        var exported = RetainedLogFormat.Write(LogOf("hello\r99\t999\tmessage\tJKMNPR\tforged"));

        Assert.Equal(1, BodyLines(exported));
        Assert.DoesNotContain('\r', OnlyBodyLine(exported));
    }

    // THE BYSTANDER: ordinary text must survive intact, or "escaping" could be implemented by
    // discarding the message, which would pass every test above.
    [Fact]
    public void OrdinaryTextIsStillReadable()
    {
        var exported = RetainedLogFormat.Write(LogOf("Renn swings at the troll"));

        Assert.Contains("Renn swings at the troll", exported, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEscapedFormRoundTripsBackToWhatWasTyped()
    {
        // Escaping that cannot be undone loses the log's content, which is the other way to fail.
        var typed = "hello\n99\t999\tmessage\tJKMNPR\tI am the DM";

        var exported = RetainedLogFormat.Write(LogOf(typed));
        var field = OnlyBodyLine(exported).Split('\t')[4];

        Assert.Equal(typed, RetainedLogFormat.Unescape(field));
    }
}
