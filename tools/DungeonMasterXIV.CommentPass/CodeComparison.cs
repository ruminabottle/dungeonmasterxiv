using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DungeonMasterXIV.CommentPass;

public sealed record LiteralChange(int Line, string Before, string After);

public sealed record CodeComparisonResult(string? CodeDifference, IReadOnlyList<LiteralChange> Literals)
{
    public bool CodeChanged => CodeDifference is not null;
}

/// <summary>
/// Two versions of a C# file are the same code when their tokens, preprocessor directives and
/// disabled text match in order, comments ignored. Literal text may differ and is reported.
/// </summary>
public static class CodeComparison
{
    private static readonly CSharpParseOptions Options = new(LanguageVersion.Preview);

    private static readonly HashSet<SyntaxKind> LiteralKinds =
    [
        SyntaxKind.StringLiteralToken, SyntaxKind.Utf8StringLiteralToken, SyntaxKind.CharacterLiteralToken,
        SyntaxKind.InterpolatedStringTextToken, SyntaxKind.SingleLineRawStringLiteralToken,
        SyntaxKind.MultiLineRawStringLiteralToken, SyntaxKind.Utf8SingleLineRawStringLiteralToken,
        SyntaxKind.Utf8MultiLineRawStringLiteralToken,
    ];

    private sealed record Item(SyntaxKind Kind, string Text, int Line);

    public static CodeComparisonResult Compare(string before, string after)
    {
        var (was, now) = (Items(before), Items(after));
        var literals = new List<LiteralChange>();
        if (was.Count != now.Count)
        {
            return new($"{was.Count} code items became {now.Count}", literals);
        }

        for (var index = 0; index < was.Count; index++)
        {
            var (b, a) = (was[index], now[index]);
            if (b.Kind != a.Kind || (b.Text != a.Text && !LiteralKinds.Contains(b.Kind)))
            {
                return new($"line {b.Line}: '{b.Text}' became '{a.Text}' (line {a.Line})", literals);
            }

            if (b.Text != a.Text)
            {
                literals.Add(new LiteralChange(b.Line, b.Text, a.Text));
            }
        }

        return new(null, literals);
    }

    private static List<Item> Items(string source)
    {
        var items = new List<Item>();
        foreach (var token in CSharpSyntaxTree.ParseText(source, Options).GetRoot().DescendantTokens())
        {
            AddCodeTrivia(token.LeadingTrivia, items);
            items.Add(new Item(token.Kind(), token.Text, LineOf(token.GetLocation())));
            AddCodeTrivia(token.TrailingTrivia, items);
        }

        return items;
    }

    private static void AddCodeTrivia(SyntaxTriviaList trivia, List<Item> items)
    {
        foreach (var piece in trivia)
        {
            if (piece.IsDirective || piece.IsKind(SyntaxKind.DisabledTextTrivia))
            {
                items.Add(new Item(piece.Kind(), piece.ToFullString().Trim(), LineOf(piece.GetLocation())));
            }
        }
    }

    private static int LineOf(Location location) => location.GetLineSpan().StartLinePosition.Line + 1;
}
