using System.Linq;
using DungeonMasterXIV.Sizes;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The counter implements the ruled procedure, and refuses everything the ruling does not cover.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ruling, cited not restated:</b> count from the first line of the class declaration to its
/// closing brace, INCLUSIVE; nothing excluded — not comments, not XML doc, not blank lines, not
/// attributes on members; attributes and doc ABOVE the declaration are outside the span.
/// </para>
/// <para>
/// <b>Fixtures are synthetic on purpose.</b> Pinning against a real file would make this fail every
/// time somebody edited that file, which trains people to change the assertion rather than read it —
/// and the number would be measuring the codebase rather than the counter.
/// </para>
/// </remarks>
public class TheClassCountIsTheRuledOneTests
{
    private static ClassSpan Single(params string[] lines)
    {
        var spans = ClassSpanReader.Read(lines);

        return Assert.Single(spans);
    }

    [Fact]
    public void TheSpanRunsFromTheDeclarationToTheClosingBraceInclusive()
    {
        var span = Single(
            "namespace N;",          // 1
            "",                      // 2
            "public class Thing",    // 3  <- declaration
            "{",                     // 4
            "    void A() { }",      // 5
            "}");                    // 6  <- closing brace

        Assert.Equal(3, span.DeclarationLine);
        Assert.Equal(6, span.ClosingBraceLine);
        Assert.Equal(4, span.Lines);
    }

    // "Nothing excluded" is the whole reason the ruling chose the less meaningful measure: a rule
    // everyone applies the same way beats one that measures better and is applied three ways.
    [Fact]
    public void CommentsDocAndBlankLinesInsideTheSpanAreCounted()
    {
        var span = Single(
            "public class Thing",    // 1
            "{",                     // 2
            "    // a comment",      // 3
            "",                      // 4
            "    /// <summary>x</summary>",  // 5
            "    [Obsolete]",        // 6
            "    void A() { }",      // 7
            "}");                    // 8

        Assert.Equal(8, span.Lines);
    }

    // The one exclusion the ruling DOES name, and it is about what sits above the declaration.
    [Fact]
    public void AttributesAndDocAboveTheDeclarationAreOutsideTheSpan()
    {
        var span = Single(
            "/// <summary>Docs.</summary>",  // 1
            "[Serializable]",                // 2
            "public class Thing",            // 3
            "{",                             // 4
            "}");                            // 5

        Assert.Equal(3, span.DeclarationLine);
        Assert.Equal(3, span.Lines);
    }

    // THE REFUSALS, and they are the design rather than a gap. The ruling names classes; putting a
    // number on a shape it does not name would encode a judgement nobody authored -- the same move
    // that made writing this tool unsafe before the convention existed, one level down.
    [Theory]
    [InlineData("public record Thing(int A);", "record")]
    [InlineData("public readonly struct Thing", "struct")]
    [InlineData("public interface IThing", "interface")]
    [InlineData("public enum Thing", "enum")]
    public void AShapeTheRulingDoesNotNameIsRefusedByName(string declaration, string kind)
    {
        var span = Single(declaration, "{", "}");

        Assert.False(span.IsMeasured);
        Assert.Equal(0, span.Lines);
        Assert.Contains(kind, span.Refusal);
    }

    [Fact]
    public void APartialClassIsRefusedBecauseOneFileCannotStateItsSpan()
    {
        var span = Single("public partial class Thing", "{", "}");

        Assert.False(span.IsMeasured);
        Assert.Contains("partial", span.Refusal);
    }

    [Fact]
    public void ANestedClassIsRefusedBecauseTheRulingDoesNotSayWhetherItCountsWithin()
    {
        var spans = ClassSpanReader.Read(new[]
        {
            "public class Outer",
            "{",
            "    public class Inner",
            "    {",
            "    }",
            "}",
        });

        var inner = Assert.Single(spans.Where(span => span.Name == "Inner"));

        Assert.False(inner.IsMeasured);
        Assert.Contains("nested", inner.Refusal);
    }

    // The control on the refusals: a refusal must not be how it handles the ordinary case, or every
    // assertion above is satisfied by a reader that refuses everything and measures nothing.
    [Fact]
    public void TheOrdinaryCaseIsStillMeasured()
    {
        var span = Single("internal sealed class Thing", "{", "}");

        Assert.True(span.IsMeasured);
        Assert.Null(span.Refusal);
        Assert.Equal(3, span.Lines);
    }
}
