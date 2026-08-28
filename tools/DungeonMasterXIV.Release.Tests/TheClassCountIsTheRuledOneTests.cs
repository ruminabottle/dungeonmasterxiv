using System.Linq;
using DungeonMasterXIV.Sizes;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The counter implements the ruled procedure and the ruled shapes, and refuses only what the
/// ruling tells it to refuse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cited, not restated:</b> "## HOW TO COUNT A CLASS" and "### THE SHAPES A REAL FILE HAS" in
/// <c>engineering-standards.md</c>. First line of the type declaration to its closing brace,
/// inclusive, nothing excluded; attributes and doc above it outside.
/// </para>
/// <para>
/// <b>Fixtures are synthetic on purpose.</b> Pinning a real file would fail whenever anyone edited
/// it, which trains people to change the assertion rather than read it — and it would measure the
/// codebase rather than the counter.
/// </para>
/// </remarks>
public class TheClassCountIsTheRuledOneTests
{
    private static ClassSpan Single(params string[] lines) => Assert.Single(ClassSpanReader.Read(lines));

    [Fact]
    public void TheSpanRunsFromTheDeclarationToTheClosingBraceInclusive()
    {
        var span = Single("namespace N;", "", "public class Thing", "{", "    void A() { }", "}");

        Assert.Equal(3, span.DeclarationLine);
        Assert.Equal(6, span.ClosingBraceLine);
        Assert.Equal(4, span.Lines);
    }

    // "Nothing excluded" is why the ruling chose the less meaningful measure: a rule everyone
    // applies the same way beats one that measures better and is applied three ways.
    [Fact]
    public void CommentsDocAndBlankLinesInsideTheSpanAreCounted() =>
        Assert.Equal(8, Single(
            "public class Thing", "{", "    // a comment", "",
            "    /// <summary>x</summary>", "    [Obsolete]", "    void A() { }", "}").Lines);

    [Fact]
    public void AttributesAndDocAboveTheDeclarationAreOutsideTheSpan()
    {
        var span = Single("/// <summary>Docs.</summary>", "[Serializable]", "public class Thing", "{", "}");

        Assert.Equal(3, span.DeclarationLine);
        Assert.Equal(3, span.Lines);
    }

    // RULED as a consequence rather than a new judgement: the declaration begins at `class Foo<T>`
    // and the constraint clauses are part of it, so they fall inside the span.
    [Fact]
    public void GenericConstraintsBetweenTheDeclarationAndTheBraceAreInside() =>
        Assert.Equal(4, Single(
            "public class Thing<T>", "    where T : IDisposable", "{", "}").Lines);

    // RULED, and it reverses this tool's first draft: if a shape is a type declaration it is under
    // the class limit. The table said "Class" because the codebase had classes when it was written.
    [Theory]
    [InlineData("public record Thing")]
    [InlineData("public readonly record struct Thing")]
    [InlineData("public struct Thing")]
    [InlineData("public interface IThing")]
    [InlineData("public enum Thing")]
    public void EveryTypeDeclarationIsMeasuredAgainstTheClassLimit(string declaration)
    {
        var span = Single(declaration, "{", "}");

        Assert.True(span.IsMeasured, $"{declaration} was refused; the ruling counts it as a class.");
        Assert.Equal(3, span.Lines);
    }

    // The edge a brace scanner gets wrong: no body at all, so there is no closing brace to find and
    // a scanner runs on into the NEXT type -- producing a number for a span that never existed.
    [Fact]
    public void ABodylessDeclarationEndsAtItsSemicolonRatherThanTheNextTypesBrace()
    {
        var spans = ClassSpanReader.Read(new[]
        {
            "public readonly record struct Entry(string A, string B);",
            "",
            "public class After",
            "{",
            "}",
        });

        Assert.Equal(2, spans.Count);
        Assert.Equal(1, spans[0].Lines);
        Assert.Equal(3, spans[1].Lines);
    }

    // RULED: counted twice, deliberately -- inside the outer span AND as its own type.
    [Fact]
    public void ANestedTypeIsCountedTwice()
    {
        var spans = ClassSpanReader.Read(new[]
        {
            "public class Outer",   // 1
            "{",                    // 2
            "    public class Inner", // 3
            "    {",                // 4
            "    }",                // 5
            "}",                    // 6
        });

        var outer = Assert.Single(spans, span => span.Name == "Outer");
        var inner = Assert.Single(spans, span => span.Name == "Inner");

        Assert.Equal(6, outer.Lines);
        Assert.Equal(3, inner.Lines);
        Assert.True(inner.DeclarationLine > outer.DeclarationLine && inner.ClosingBraceLine < outer.ClosingBraceLine);
    }

    // RULED: each type gets its own span against the class limit; the file gets the file limit.
    [Fact]
    public void SeveralTypesInOneFileEachGetTheirOwnSpan()
    {
        var spans = ClassSpanReader.Read(new[]
        {
            "public class A", "{", "}", "", "public class B", "{", "}",
        });

        Assert.Equal(2, spans.Count);
        Assert.All(spans, span => Assert.Equal(3, span.Lines));
    }

    // THE ONE REFUSAL THE RULING DEMANDS, and the one this tool could most easily get wrong.
    // Summing needs every part and this reads one file. Reporting the part it can see would not be
    // an underestimate -- it would look exactly like an answer, arrive under the limit, and be wrong
    // in the reassuring direction.
    [Fact]
    public void APartialTypeIsRefusedByNameAndCarriesNoNumber()
    {
        var span = Single("public partial class Thing", "{", "    void A() { }", "}");

        Assert.False(span.IsMeasured);
        Assert.Equal(0, span.Lines);
        Assert.Equal("Thing", span.Name);
        Assert.Contains("SUM of its parts", span.Refusal);
    }

    // The control on the refusal: refusing must not be how it handles ordinary types, or every
    // assertion here is satisfied by a reader that refuses everything and measures nothing.
    [Fact]
    public void TheOrdinaryCaseIsStillMeasured()
    {
        var span = Single("internal sealed class Thing", "{", "}");

        Assert.True(span.IsMeasured);
        Assert.Null(span.Refusal);
        Assert.Equal(3, span.Lines);
    }

    // And a declaration quoted in prose is not a declaration.
    [Fact]
    public void ATypeNamedInsideACommentIsNotCounted() =>
        Assert.Empty(ClassSpanReader.Read(new[] { "// public class NotReal", "/// public class AlsoNot" }));
}
