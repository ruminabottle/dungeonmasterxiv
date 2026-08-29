using System.Linq;
using DungeonMasterXIV.Sizes;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The method, parameter and nesting rows — the three the tool did not measure (DMXENG-55).
/// </summary>
/// <remarks>
/// <para>
/// <b>EVERY TEST HERE IS A POSITIVE CONTROL ON A ROW THAT PREVIOUSLY HAD NO INSTRUMENT.</b> A new
/// measurement that has never been seen to move is indistinguishable from no measurement at all,
/// and that is not a general principle here — it is the specific thing that happened: the tool
/// reported clean on two rows for months while a seven-parameter constructor merged through a
/// review conducted with unusual attention to size.
/// </para>
/// <para>
/// <b>Each ruling is tested as a ruling, not as an implementation detail.</b> Primary constructors
/// counting, expression bodies being methods, and lambdas not adding depth are the Deployment
/// Manager's three answers; if any is reversed, the test that fails should be the one named for it.
/// </para>
/// </remarks>
public class TheOtherThreeRowsAreMeasuredTests
{
    // THE PARAMETER ROW, AND THE BREACH THAT CAUSED THIS TICKET. HostRunner's constructor took seven
    // against a block of six and nothing saw it. Fails if the row goes back to being unmeasured.
    [Fact]
    public void ASevenParameterConstructorIsCounted()
    {
        var span = Only(MemberReader.Read(
            """
            class Thing
            {
                public Thing(int a, int b, int c, int d, int e, int f, int g)
                {
                }
            }
            """));

        Assert.Equal(7, span.Parameters);
    }

    // RULING 1: a primary constructor's list counts. It is the type's construction surface, and
    // ruling otherwise would make the row evadable by syntax choice.
    //
    // Fails if a primary constructor is skipped -- which is the shape that would let the same breach
    // through again in a different syntax.
    [Fact]
    public void APrimaryConstructorsListCountsTowardTheParameterRow()
    {
        var spans = MemberReader.Read("class Thing(int a, int b, int c, int d, int e, int f, int g);");

        var primary = Assert.Single(spans, span => span.Name.Contains("primary constructor"));
        Assert.Equal(7, primary.Parameters);
    }

    // A record's positional list is a primary constructor too, and it is the form this codebase
    // actually uses. Without this, ruling 1 would be honoured for classes and quietly not for records.
    [Fact]
    public void ARecordsPositionalListCountsTheSameWay()
    {
        var spans = MemberReader.Read("public readonly record struct Wide(int A, int B, int C, int D, int E);");

        Assert.Equal(5, Assert.Single(spans).Parameters);
    }

    // THE METHOD ROW. Declaration line to end, inclusive -- the class row's procedure applied to a
    // member. The body here is deliberately trivial: the row measures LENGTH, not complexity.
    [Fact]
    public void AMethodIsMeasuredFromItsDeclarationToItsEnd()
    {
        var body = string.Join("\n", Enumerable.Repeat("        var x = 1;", 8));
        var span = Only(MemberReader.Read($"class Thing\n{{\n    void Long()\n    {{\n{body}\n    }}\n}}"));

        // declaration, brace, eight statements, brace.
        Assert.Equal(11, span.Lines);
    }

    // ATTRIBUTES AND DOC COMMENTS SIT OUTSIDE THE SPAN, matching the class ruling verbatim:
    // "attributes and doc comments sitting ABOVE the declaration are outside the span".
    //
    // Fails if leading trivia is counted -- which would inflate every documented member in a
    // codebase where the documentation is routinely longer than the code.
    [Fact]
    public void DocCommentsAndAttributesAboveAMemberAreOutsideItsSpan()
    {
        var span = Only(MemberReader.Read(
            """
            class Thing
            {
                /// <summary>Four lines of documentation.</summary>
                /// <remarks>Which this codebase has a great deal of.</remarks>
                [Obsolete]
                void Short()
                {
                }
            }
            """));

        Assert.Equal(3, span.Lines);
    }

    // RULING 2: an expression-bodied member is a method for this row. The Deployment Manager said it
    // "will almost never bind, and the case where it does is exactly the one worth catching" -- so
    // this is the case where it does.
    [Fact]
    public void AnExpressionBodiedMemberIsAMethod()
    {
        var chain = string.Join("\n", Enumerable.Repeat("        .Where(x => x > 0)", 70));
        var span = Only(MemberReader.Read($"class Thing\n{{\n    object Big() =>\n        Source\n{chain};\n}}"));

        Assert.True(span.Lines > 60, $"an expression body of {span.Lines} lines should be over the block");
    }

    // THE NESTING ROW. Four real levels a reader has to hold open at once.
    [Fact]
    public void NestedControlFlowIsCountedByLevel()
    {
        var span = Only(MemberReader.Read(
            """
            class Thing
            {
                void Pyramid()
                {
                    if (A) { foreach (var x in B) { while (C) { if (D) { Act(); } } } }
                }
            }
            """));

        Assert.Equal(4, span.Depth);
    }

    // AN else if IS ONE DECISION CONTINUED, not a decision inside a decision. In the syntax tree the
    // second `if` is a child of the `else`, so this is a real case rather than a hypothetical -- and
    // counting it would make every ordinary dispatch chain read as a pyramid.
    [Fact]
    public void AnElseIfDoesNotAddALevel()
    {
        var span = Only(MemberReader.Read(
            """
            class Thing
            {
                void Chain()
                {
                    if (A) { One(); }
                    else if (B) { Two(); }
                    else if (C) { Three(); }
                    else { Four(); }
                }
            }
            """));

        Assert.Equal(1, span.Depth);
    }

    // RULING 3, AND THE DEPLOYMENT MANAGER HOLDS THIS ONE LOOSELY AND SAID SO. A lambda body does not
    // add depth by itself; control flow inside counts from the lambda's own baseline, because a
    // lambda is usually what FLATTENS a pyramid and charging it would penalise the fix.
    //
    // Fails if lambdas start contributing depth. IF THIS RULING IS REVERSED, THIS IS THE TEST THAT
    // SHOULD CHANGE -- it is named for the ruling rather than for the mechanism, on purpose.
    [Fact]
    public void ALambdaBodyDoesNotAddNestingDepth()
    {
        var span = Only(MemberReader.Read(
            """
            class Thing
            {
                void Flat()
                {
                    foreach (var x in B)
                    {
                        Run(() => { if (A) { Act(); } });
                    }
                }
            }
            """));

        // The foreach is one level. The `if` inside the lambda restarts at the lambda's baseline.
        Assert.Equal(1, span.Depth);
    }

    // A LOCAL FUNCTION IS REFUSED BY NAME, because the Deployment Manager DELIBERATELY did not rule
    // on how one counts -- its own member, or part of its container's length and depth.
    //
    // Fails if it is silently measured or silently skipped. Both would settle an open question by
    // implementation, which is the move that made writing this tool unsafe before the counting
    // convention was written down.
    [Fact]
    public void ALocalFunctionIsRefusedRatherThanCountedEitherWay()
    {
        var spans = MemberReader.Read(
            """
            class Thing
            {
                void Outer()
                {
                    void Inner(int a) { }
                    Inner(1);
                }
            }
            """);

        var local = Assert.Single(spans, span => !span.IsMeasured);
        Assert.Contains("Inner", local.Name);
        Assert.Contains("NOT RULED", local.Refusal!);
    }

    // The unmeasured population is stated rather than left to be noticed. An accessor body may well
    // be a "method" -- nobody has ruled -- and this pins that the tool does not pretend either way.
    [Fact]
    public void PropertyAccessorsAreNotCountedAsMethods()
    {
        var spans = MemberReader.Read(
            """
            class Thing
            {
                public int Value
                {
                    get { return 1; }
                    set { _v = value; }
                }
            }
            """);

        Assert.Empty(spans);
    }

    private static MemberSpan Only(System.Collections.Generic.IReadOnlyList<MemberSpan> spans) =>
        Assert.Single(spans, span => span.IsMeasured);
}
