using System.IO;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-183 — the FILE row counts LINES, and a trailing newline terminates the last one rather than
/// beginning another.
/// </summary>
/// <remarks>
/// <para>
/// <b>The disagreement band was exactly one value wide.</b> <c>Split('\n')</c> on a
/// newline-terminated file yields a trailing empty element that is not a line, so the gate read one
/// higher than the Sizes CLI. At 448 and 449 both called it compliant; at 451 and above both
/// refused. <b>Only at 450 — the block itself — did the verdicts differ</b>, and no file in the tree
/// has ever sat on 450, which is why this survived.
/// </para>
/// <para>
/// <b>So these tests sit ON the boundary, not near it.</b> Every existing file-row fixture in this
/// assembly is built with <c>string.Join('\n', ...)</c> and therefore carries NO trailing newline —
/// phantom-free by accident of construction, and green against the defect. A test written near the
/// boundary, or one built the same way, would have passed against this bug. That is the lesson the
/// ticket asked to be encoded.
/// </para>
/// <para>
/// <b>The ruling being pinned</b> is <c>Program.cs:67</c> — "a file is every line in it, first to
/// last" — and its operational half in <c>engineering-standards.md</c>: a trailing newline
/// TERMINATES the last line, it does not BEGIN a new one. <c>File.ReadAllLines</c> matches
/// <c>wc -l</c>; <c>Split('\n')</c> matches neither, so the gate was the defective instrument and
/// the CLI is the reference.
/// </para>
/// </remarks>
public class TheFileRowCountsLinesNotNewlinesTests
{
    private const string Path = "Fixture.cs";

    /// <summary>A source of exactly <paramref name="lines"/> lines, terminated or not.</summary>
    private static string Lines(int lines, bool trailingNewline) =>
        string.Join('\n', Enumerable.Repeat("// line", lines)) + (trailingNewline ? "\n" : string.Empty);

    /// <summary>What the gate measured for the file row, or null when it recorded nothing.</summary>
    private static int? FileValue(string source) =>
        SizeGate.FlagCrossingsIn(Path, source)
            .Breaches.SingleOrDefault(b => b.Row == SizeGate.FileRow)?.Value;

    private static bool RefusedForFileBlock(string source) =>
        SizeGate.BreachesIn(Path, source).Breaches.Any(b => b.Row == SizeGate.FileRow);

    // ---------- the one value where the two instruments disagreed ----------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFileOfExactly450LinesCountsAs450(bool trailingNewline)
    {
        // 450 crosses the FILE FLAG of 300, so the count itself is observable even though the file
        // is compliant at the block and records no breach there.
        Assert.Equal(450, FileValue(Lines(450, trailingNewline)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFileOfExactly450LinesIsCompliantAtTheBlockWithMarginZero(bool trailingNewline)
    {
        var source = Lines(450, trailingNewline);

        Assert.False(RefusedForFileBlock(source));
        // Margin 0 stated as the arithmetic rather than implied by the absence of a refusal: the
        // engineer reads "450 lines, margin 0, compliant" from the CLI and the gate must agree.
        Assert.Equal(0, SizeGate.FileBlock - FileValue(source));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFileOfExactly451LinesIsRefusedAndCountedAs451(bool trailingNewline)
    {
        var source = Lines(451, trailingNewline);

        Assert.True(RefusedForFileBlock(source));
        // Value pinned, not just the refusal: counting 452 would refuse this file for the wrong
        // reason and would still look like a pass.
        Assert.Equal(451, SizeGate.BreachesIn(Path, source).Breaches.Single(b => b.Row == SizeGate.FileRow).Value);
    }

    /// <summary>
    /// The negative control the ticket required: files WITHOUT a trailing newline agreed before this
    /// fix and must keep agreeing. A suite that only proved the terminated case could not tell a fix
    /// from a change of subject.
    /// </summary>
    [Theory]
    [InlineData(448)]
    [InlineData(449)]
    [InlineData(450)]
    [InlineData(451)]
    [InlineData(452)]
    public void TerminatingTheLastLineDoesNotChangeTheCount(int lines)
    {
        Assert.Equal(FileValue(Lines(lines, false)), FileValue(Lines(lines, true)));
    }

    /// <summary>
    /// The ruling itself, pinned against the reference implementation rather than against a number I
    /// chose: the gate must count what <see cref="File.ReadAllLines"/> counts, which is what the
    /// Sizes CLI uses and what <c>wc -l</c> agrees with.
    /// </summary>
    [Theory]
    [InlineData(450, true)]
    [InlineData(450, false)]
    [InlineData(1, true)]
    [InlineData(0, true)]
    public void TheGateCountsWhatReadAllLinesCounts(int lines, bool trailingNewline)
    {
        var source = lines == 0 ? string.Empty : Lines(lines, trailingNewline);
        var temporary = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllText(temporary, source);
            var reference = File.ReadAllLines(temporary).Length;

            // A zero-line file records no crossing at all, so compare through the same null.
            Assert.Equal(reference > SizeGate.FileFlag ? reference : (int?)null, FileValue(source));
            Assert.Equal(lines, reference);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    // ---------- the class row must NOT move, and this is the evidence ----------

    /// <summary>
    /// A class is a SPAN between two known lines, so it cannot pick up a trailing phantom — but the
    /// same array feeds <c>ClassSpanReader</c>, so "cannot" is asserted here rather than argued.
    /// DMXENG-128 depends on <c>SessionCoordinator</c> reading 400/400, margin 0.
    /// </summary>
    [Fact]
    public void TheClassRowIsUnmovedByATrailingNewline()
    {
        const int Filler = 300;
        var body = string.Join('\n', Enumerable.Repeat("    // line", Filler));
        var declared = $"namespace F;\npublic sealed class Spanning\n{{\n{body}\n}}";

        static int? ClassValue(string source) =>
            SizeGate.FlagCrossingsIn(Path, source)
                .Breaches.SingleOrDefault(b => b.Row == SizeGate.ClassRow)?.Value;

        // Declaration line, opening brace, the filler, and the closing brace.
        Assert.Equal(Filler + 3, ClassValue(declared));
        Assert.Equal(ClassValue(declared), ClassValue(declared + "\n"));
    }
}
