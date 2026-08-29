using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DungeonMasterXIV.Sizes;

/// <summary>
/// Reads the method, parameter and nesting rows out of one file (DMXENG-55).
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS PARSES RATHER THAN MATCHES, WHICH IS A DEPARTURE FROM
/// <see cref="ClassSpanReader"/> AND IS DELIBERATE.</b> The standards say in their own words that
/// "the obvious implementation of this rule is wrong", and back it with a measurement: a hand-rolled
/// class count was wrong on <b>5 of 81 files, in both directions</b>. A method declaration, a
/// parameter list and a nesting level are all harder to recognise from a line than a type
/// declaration is — generic constraints, multi-line signatures, expression bodies, nested lambdas.
/// <b>This ticket exists because an approximate instrument became the rule; three more
/// approximations would rebuild that at one remove.</b>
/// </para>
/// <para>
/// <b>The two original rows were NOT moved onto this parser.</b> They are ruled, tested and already
/// quoted in ticket text and PR bodies. Re-deriving them through a different reader would silently
/// change numbers people have cited, which is a change nobody asked for and nobody would see.
/// </para>
/// <para>
/// <b>WHAT THIS ROW COVERS, STATED HERE AND PRINTED ON EVERY RUN.</b> Methods, constructors,
/// operators, conversion operators and finalizers. <b>Property, indexer and event accessors are NOT
/// measured</b> — the standards say "Method", and whether an accessor body is one is a question
/// nobody has ruled. Saying so is the point: an unstated exclusion is how two rows became five.
/// </para>
/// <para>
/// <b>A LOCAL FUNCTION IS REFUSED BY NAME.</b> The Deployment Manager deliberately did not rule on
/// how one counts — whether it is its own member, or part of its container's length and depth. This
/// refuses and says which, exactly as <see cref="ClassSpanReader"/> refuses a partial type, because
/// the alternative is that this file settles an open question by implementation.
/// </para>
/// </remarks>
public static class MemberReader
{
    /// <summary>Every measurable member in one file, measured or refused by name.</summary>
    public static IReadOnlyList<MemberSpan> Read(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var spans = new List<MemberSpan>();

        foreach (var node in root.DescendantNodes())
        {
            var span = Describe(node);

            if (span is not null)
            {
                spans.Add(span);
            }
        }

        return spans;
    }

    private static MemberSpan? Describe(SyntaxNode node) => node switch
    {
        // Refused before anything is measured, so an unruled shape cannot be silently folded into
        // the numbers for the member that contains it.
        LocalFunctionStatementSyntax local => new MemberSpan(
            Name(local.Identifier.Text, local.ParameterList),
            LineOf(local),
            0,
            0,
            0,
            "local function: whether it is its own member or part of its container is NOT RULED"),

        // A primary constructor has no body, so the method and nesting rows do not apply to it --
        // but the PARAMETER row does, and that is the whole reason it is here.
        TypeDeclarationSyntax { ParameterList: { } primary } type => new MemberSpan(
            Name(type.Identifier.Text, primary) + " (primary constructor)",
            LineOf(type),
            0,
            primary.Parameters.Count,
            0,
            null),

        ConstructorDeclarationSyntax constructor => Measured(
            constructor, Name(constructor.Identifier.Text, constructor.ParameterList), constructor.ParameterList),

        MethodDeclarationSyntax method => Measured(
            method, Name(method.Identifier.Text, method.ParameterList), method.ParameterList),

        OperatorDeclarationSyntax op => Measured(
            op, Name("operator " + op.OperatorToken.Text, op.ParameterList), op.ParameterList),

        ConversionOperatorDeclarationSyntax conversion => Measured(
            conversion, Name("operator " + conversion.Type, conversion.ParameterList), conversion.ParameterList),

        DestructorDeclarationSyntax destructor => Measured(
            destructor, "~" + destructor.Identifier.Text + "()", destructor.ParameterList),

        // Delegates declare a parameter list and no body. The parameter row applies; the other two
        // have nothing to measure, which is different from measuring zero.
        DelegateDeclarationSyntax @delegate => new MemberSpan(
            Name(@delegate.Identifier.Text, @delegate.ParameterList),
            LineOf(@delegate),
            0,
            @delegate.ParameterList.Parameters.Count,
            0,
            null),

        _ => null,
    };

    private static MemberSpan Measured(SyntaxNode node, string name, BaseParameterListSyntax parameters) =>
        new(name, LineOf(node), LinesOf(node), parameters.Parameters.Count, DepthOf(node), null);

    private static string Name(string identifier, BaseParameterListSyntax parameters) =>
        $"{identifier}({parameters.Parameters.Count})";

    /// <summary>
    /// The first line of the declaration itself, with any attribute list excluded.
    /// </summary>
    /// <remarks>
    /// <b>ATTRIBUTES ARE PART OF THE SYNTAX NODE AND MUST NOT BE PART OF THE SPAN, AND THAT TRAP IS
    /// WORTH NAMING.</b> Roslyn attaches a doc comment as leading TRIVIA, which
    /// <see cref="SyntaxNode.GetLocation"/> already excludes — but an <c>AttributeList</c> is a
    /// CHILD NODE, so the node's own start line is the <c>[Attribute]</c> line. The class ruling is
    /// explicit that "attributes and doc comments sitting ABOVE the declaration are outside the
    /// span", so taking the node's start would have counted attributes for members while the class
    /// row excludes them for types — the two rows disagreeing about the same rule.
    /// <para>
    /// Caught by a test that expected 3 and got 4. Trusting the parser to mean what the ruling means
    /// is the same class of error as trusting a regex to.
    /// </para>
    /// </remarks>
    private static int LineOf(SyntaxNode node) =>
        StartOf(node).GetLineSpan().StartLinePosition.Line + 1;

    private static Location StartOf(SyntaxNode node)
    {
        var attributes = node switch
        {
            MemberDeclarationSyntax member => member.AttributeLists,
            LocalFunctionStatementSyntax local => local.AttributeLists,
            _ => default,
        };

        if (attributes.Count == 0)
        {
            return node.GetLocation();
        }

        // The first token that is not part of an attribute list -- the modifier, or the return type.
        var afterAttributes = node.ChildNodesAndTokens()
            .FirstOrDefault(child => child.AsNode() is not AttributeListSyntax);

        return afterAttributes == default ? node.GetLocation() : afterAttributes.GetLocation()!;
    }

    /// <summary>
    /// Declaration line to end, inclusive — the class row's procedure applied to a member.
    /// </summary>
    /// <remarks>
    /// <b>Measured from the node, so attributes and doc comments sit outside the span</b>, matching
    /// the class ruling: "attributes and doc comments sitting ABOVE the declaration are outside".
    /// Roslyn attaches those as leading trivia, which <see cref="SyntaxNode.GetLocation"/> excludes.
    /// </remarks>
    private static int LinesOf(SyntaxNode node)
    {
        var start = StartOf(node).GetLineSpan().StartLinePosition.Line;
        var end = node.GetLocation().GetLineSpan().EndLinePosition.Line;
        return end - start + 1;
    }

    /// <summary>
    /// The deepest nesting of control flow inside one member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Counted over control flow, not over braces.</b> The row exists to stop unreadable
    /// conditional pyramids, and a brace count would charge a member for its own body, an object
    /// initialiser or a nested type — none of which is what makes a pyramid hard to read.
    /// </para>
    /// <para>
    /// <b>A LAMBDA RESETS THE BASELINE, which is the Deployment Manager's ruling and the one they
    /// hold loosely.</b> Control flow inside a lambda counts from that lambda's own zero, so a
    /// method whose body is one <c>foreach</c> containing a lambda containing an <c>if</c> measures
    /// 1 and not 2. Their reasoning: a lambda is usually the thing that FLATTENS a pyramid, and
    /// counting its contents would penalise the fix and reward the pyramid. <b>If it starts hiding
    /// real nesting, that case goes back to them.</b>
    /// </para>
    /// <para>
    /// <b>An <c>else if</c> does not add a level</b>, because it is one decision continued rather
    /// than a decision inside a decision — the reader is not holding a second condition open. In the
    /// syntax tree the second <c>if</c> is a child of the <c>else</c>, so this is a real case that
    /// had to be handled rather than a hypothetical.
    /// </para>
    /// </remarks>
    private static int DepthOf(SyntaxNode member) =>
        member is BaseMethodDeclarationSyntax { Body: { } body } ? Depth(body, 0) : 0;

    private static int Depth(SyntaxNode node, int here)
    {
        var deepest = here;

        foreach (var child in node.ChildNodes())
        {
            // The ruling: whatever a lambda contains starts again from this level.
            var baseline = child is AnonymousFunctionExpressionSyntax ? 0 : here;
            var inside = Nests(child) ? baseline + 1 : baseline;

            deepest = Math.Max(deepest, Math.Max(inside, Depth(child, inside)));
        }

        return deepest;
    }

    /// <summary>Whether this node is a level a reader has to hold open.</summary>
    private static bool Nests(SyntaxNode node) => node switch
    {
        // One decision continued, not a decision inside a decision.
        IfStatementSyntax when node.Parent is ElseClauseSyntax => false,

        IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax
            or DoStatementSyntax or SwitchStatementSyntax or TryStatementSyntax or LockStatementSyntax
            or FixedStatementSyntax or UnsafeStatementSyntax => true,

        // Only the block form. `using var x = ...;` introduces no level for a reader to hold.
        UsingStatementSyntax { Statement: not null } => true,

        _ => false,
    };
}
