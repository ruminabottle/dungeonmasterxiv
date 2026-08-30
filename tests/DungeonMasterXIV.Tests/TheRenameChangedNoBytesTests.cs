using System;
using System.Security.Cryptography;
using System.Text;
using DungeonMasterXIV.Data;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// DMXENG-114's proof obligation: renaming <c>LogExport</c> to <see cref="RetainedLogFormat"/>
/// changed the NAME and <b>not one byte of the output</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A TEST ASSERTING THE NEW NAME COMPILES PROVES THE RENAME HAPPENED. ONLY A BYTE COMPARISON
/// PROVES IT CHANGED NOTHING.</b> Every other test in this suite would keep passing through a rename
/// that also altered a header, dropped a field or reordered the escape chain, because they were all
/// updated to the new name in the same edit.
/// </para>
/// <para>
/// <b>THE GOLDEN WAS CAPTURED AT THE PRE-RENAME REF, AND THAT IS THE WHOLE VALUE OF IT.</b> Taken
/// from <c>LogExport.Write</c> at <c>e5b56e1</c> — before this branch touched anything — 327 bytes,
/// <c>sha256 d59458ccb313a66fde0860a1e7114c652e6fe17e1d3d94b78cf4dc7123d29613</c>. <b>A golden
/// regenerated from the post-rename build would prove nothing</b>: both sides would come from the
/// same code, so it would pass against any output at all, including a broken one. It must never be
/// refreshed to make this test pass — a failure here means the bytes moved, which is the finding.
/// </para>
/// <para>
/// <b>The separator is <see cref="Environment.NewLine"/> rather than a literal, and that is not a
/// weakening.</b> <c>StringBuilder.AppendLine</c> writes the platform's newline, so a golden with
/// hard <c>\n</c> would fail on Windows for a reason that has nothing to do with this rename. Every
/// field value and every escape below is pinned literally; only the line separator is symbolic.
/// <b>That the written format's line ending varies by the machine that wrote it is a real property
/// of this format and is NOT this ticket's to change</b> — reported separately.
/// </para>
/// </remarks>
public class TheRenameChangedNoBytesTests
{
    // The captured golden, line by line. Real tabs are \t; the doubled forms in the third entry are
    // the ESCAPED text -- a literal backslash followed by t, n and a backslash, which is what
    // Escape produces from a tab, a newline and a backslash the user actually typed.
    private static readonly string[] Golden =
    [
        "# DungeonMasterXIV session log",
        "version: 1",
        "campaign: 7f3a1c88-0d2e-4b6a-9c11-5e8d2f40a913",
        "ended: 638000000000000000",
        "",
        "1\t638000000000000001\tmessage\tBCDFGH\tRenn swings at the troll",
        "2\t638000000000000002\troll\tJKMNPR\t4d6dl1+2",
        "3\t638000000000000003\tmessage\tBCDFGH\ta\\ttab, a\\nnewline and a \\\\ backslash",
        "4\t638000000000000004\tleft\tJKMNPR\t",
    ];

    private static string Expected => string.Join(Environment.NewLine, Golden) + Environment.NewLine;

    [Fact]
    public void TheOutputIsByteIdenticalToTheCaptureTakenBeforeTheRename()
    {
        Assert.Equal(Expected, RetainedLogFormat.Write(GoldenFixture.Log()));
    }

    // THE POSITIVE CONTROL. A byte comparison that cannot fail is not a byte comparison -- and this
    // one is built from the same array the assertion above uses, so it fails if that array is ever
    // edited into something the comparison cannot distinguish.
    [Fact]
    public void TheComparisonFailsIfASingleByteMoves()
    {
        var tampered = Expected.Replace("version: 1", "version: 2", StringComparison.Ordinal);

        Assert.NotEqual(tampered, RetainedLogFormat.Write(GoldenFixture.Log()));
        Assert.Equal(Expected.Length, tampered.Length);
    }

    // The fixture must exercise the format, or "byte-identical" is proven of a case with no format
    // in it: a header, a version, several entries, and text carrying all three escaped characters.
    [Fact]
    public void TheFixtureActuallyExercisesTheEscaping()
    {
        var written = RetainedLogFormat.Write(GoldenFixture.Log());

        Assert.Contains("\\t", written, StringComparison.Ordinal);
        Assert.Contains("\\n", written, StringComparison.Ordinal);
        Assert.Contains("\\\\", written, StringComparison.Ordinal);
        Assert.Equal(4, GoldenFixture.Log().Entries.Count);
    }

    // The captured hash, asserted against the SAME bytes the golden describes. This is what a future
    // reader can re-derive without trusting this file's array transcription.
    [Fact]
    public void TheGoldenMatchesTheHashRecordedWhenItWasCaptured()
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Expected.Replace("\r\n", "\n", StringComparison.Ordinal))));

        Assert.Equal("d59458ccb313a66fde0860a1e7114c652e6fe17e1d3d94b78cf4dc7123d29613", sha);
    }
}
