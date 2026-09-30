using System.Text.RegularExpressions;

namespace DungeonMasterXIV.CommentPass;

/// <summary>
/// References that point only into the retired agent system, and forms worth a second look.
/// Every pattern is assembled from pieces so this file never matches its own guard.
/// </summary>
public static class DeadPatterns
{
    private const string Dash = "-";
    private const string NotAfterWordChar = "(?<![A-Za-z0-9])";

    /// <summary>A hit here is a dead reference, full stop.</summary>
    public static readonly IReadOnlyList<Regex> Guard =
    [
        new(NotAfterWordChar + "(?:BUG|DMXENG|SQ|PRD|E|T)" + Dash + @"\d+"),
        new("[Ss]pec" + " [Oo]wner"),
        new("[Dd]eployment" + " [Mm]anager"),
        new("[Pp]roduct" + " [Oo]wner"),
        new("[Ee]ngineering" + " [Ll]ead"),
        new("[Cc]ode" + " [Rr]eviewer"),
        new("[Bb]" + "reakfix"),
        new("the " + "HUMAN"),
        new(@"\." + "claude/"),
        new("engineering" + Dash + "standards"),
        new("product" + Dash + "directives"),
        new(@"\bbrief" + @"\.md"),
        new(NotAfterWordChar + "[Qq][Aa]" + Dash + @"\d"),
    ];

    /// <summary>A hit here is rewritten or deliberately kept, and a kept one is listed in the PR.</summary>
    public static readonly IReadOnlyList<Regex> Review =
    [
        new(@"[Dd]ecision \d+"),
        new(@"\bQA\b"),
        new(@"\bthe human\b"),
        new(@"(?<![&\w])#\d{2,3}\b"),
        new(@"\b[0-9a-f]{7}\b"),
        new(@"\bticket"),
        new(@"\b(?:the|that|this) (?:bug|ticket|ruling|escalation|brief)\b"),
        new(@"[Bb]ug ?\d+"),
        new(@"\bthe (?:standards|PRD|brief)\b"),
        new(@"\bC\d{1,2}\b"),
    ];

    /// <summary>Every match of any pattern in the set, with its one-based line.</summary>
    public static IReadOnlyList<(int Line, string Match)> Find(string text, IReadOnlyList<Regex> set)
    {
        var hits = new List<(int Line, string Match)>();
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var pattern in set)
            {
                foreach (Match match in pattern.Matches(lines[index]))
                {
                    hits.Add((index + 1, match.Value));
                }
            }
        }

        return hits;
    }
}
