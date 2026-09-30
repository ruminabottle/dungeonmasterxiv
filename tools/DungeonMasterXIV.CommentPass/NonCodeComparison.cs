using System.Text.RegularExpressions;

namespace DungeonMasterXIV.CommentPass;

/// <summary>
/// Two versions of a non-C# file are the same when they match once their comments are removed.
/// A file type with no rule here is always a difference, so an unexpected edit cannot pass.
/// </summary>
public static class NonCodeComparison
{
    private static readonly Regex XmlComment = new("<!--.*?-->", RegexOptions.Singleline);

    public static string? Difference(string path, string before, string after)
    {
        var meaningful = RuleFor(path);
        if (meaningful is null)
        {
            return $"no comparison rule for {path}";
        }

        var (was, now) = (meaningful(before), meaningful(after));
        for (var index = 0; index < Math.Max(was.Count, now.Count); index++)
        {
            var (b, a) = (index < was.Count ? was[index] : "<end>", index < now.Count ? now[index] : "<end>");
            if (b != a)
            {
                return $"'{b}' became '{a}'";
            }
        }

        return null;
    }

    private static Func<string, List<string>>? RuleFor(string path)
    {
        var name = Path.GetFileName(path);
        return Path.GetExtension(path) switch
        {
            ".csproj" or ".targets" or ".props" => text => Lines(XmlComment.Replace(text, string.Empty).Split('\n')),
            _ when name is "Dockerfile" or ".gitignore" or ".dockerignore" or "size-gate-baseline.txt" =>
                text => Lines(text.Split('\n').Where(line => !line.TrimStart().StartsWith('#'))),
            _ when name is "ClockFormsFixture.txt" => text => Lines(AfterHeader(text.Split('\n'))),
            _ => null,
        };
    }

    /// <summary>Everything after the second blank line: the title and header paragraph are prose.</summary>
    private static IEnumerable<string> AfterHeader(string[] lines)
    {
        var blanks = 0;
        var index = 0;
        while (index < lines.Length && blanks < 2)
        {
            blanks += lines[index].Trim().Length == 0 ? 1 : 0;
            index++;
        }

        return lines.Skip(index);
    }

    private static List<string> Lines(IEnumerable<string> lines) =>
        [.. lines.Select(line => line.TrimEnd()).Where(line => line.Length > 0)];
}
