using System.Text.RegularExpressions;

namespace DungeonMasterXIV.CommentPass;

/// <summary>
/// Deletes retired ticket IDs only where nothing but citation surrounds them: a parenthesis holding
/// only such IDs, or one that opens or closes a list of citations. Anything inside a sentence is
/// left for a person.
/// </summary>
public static class TagStripper
{
    private const string Id = "(?<![A-Za-z0-9])(?:BUG|DMXENG|SQ|E|T)" + "-" + @"\d+";

    private static readonly Regex Pure = new($@" ?\({Id}(?:, ?{Id})*\)");
    private static readonly Regex OnlyAMarker = new(@"^[ \t]*(?://|\*)/?[ \t]*$");
    private static readonly Regex Leading = new($@"\({Id}, ?");
    private static readonly Regex Trailing = new($@", ?{Id}(?=[,)])");

    public static string Strip(string text)
    {
        var result = Pure.Replace(text, tag => IsPinnedToTheMarker(text, tag) ? tag.Value : string.Empty);
        string previous;
        do
        {
            previous = result;
            result = Trailing.Replace(Leading.Replace(result, "("), string.Empty);
        }
        while (result != previous);

        return result;
    }

    // "/// (TAG).</b>" has nowhere to put the punctuation once the tag goes, so a person decides.
    private static bool IsPinnedToTheMarker(string text, Match tag)
    {
        var end = tag.Index + tag.Length;
        if (end >= text.Length || !".,;:!?".Contains(text[end]))
        {
            return false;
        }

        var lineStart = text.LastIndexOf('\n', Math.Max(tag.Index - 1, 0)) + 1;
        return OnlyAMarker.IsMatch(text[lineStart..tag.Index]);
    }
}
