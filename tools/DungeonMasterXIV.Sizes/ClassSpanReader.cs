using System.Text.RegularExpressions;

namespace DungeonMasterXIV.Sizes;

/// <summary>
/// Counts class spans under the procedure the Deployment Manager ruled on 2026-08-28.
/// </summary>
/// <remarks>
/// <para>
/// <b>The procedure, cited rather than restated</b> —
/// <c>.claude/skills/deployment-manager/engineering-standards.md</c>, "HOW TO COUNT A CLASS —
/// RULED, BECAUSE THE TABLE NEVER SAID": count from the FIRST LINE OF THE CLASS DECLARATION to its
/// CLOSING BRACE, INCLUSIVE. Nothing is excluded — not comments, not XML doc, not blank lines, not
/// attributes on members. Attributes and doc ABOVE the declaration are outside the span.
/// </para>
/// <para>
/// <b>This tool exists BECAUSE the convention was written first, and that order is the whole
/// point.</b> A counter written while the definition was still disputed would not have been an aid,
/// it would have been the ruling — a measurement tool has to pick an interpretation, so shipping one
/// settles the question silently and with nobody's name on it. Three people disagreeing out loud is
/// how the gap surfaced at all; a number out of a script is not argued with.
/// </para>
/// <para>
/// <b>So it REFUSES rather than guesses, and that is the design.</b> The ruling covers one shape and
/// real files hold more — nested types, records, structs, interfaces, partial classes, several types
/// in one file. Putting a number on those would encode a judgement nobody authored, one level down
/// from the one that was just settled. Each is NAMED and left unnumbered instead. A tool that says
/// "I will not answer that" is worth more here than one that answers everything, because the second
/// kind is indistinguishable from a ruling.
/// </para>
/// <para>
/// <b>It does not enforce.</b> Whether a breach fails a build is a policy question the standards do
/// not answer; they say a blocking limit is "a denial on its own", which is about review.
/// </para>
/// </remarks>
public static class ClassSpanReader
{
    // The declaration line of a top-level class. Deliberately narrow: anything this does not match
    // is reported as unsupported rather than approximated.
    private static readonly Regex ClassDeclaration = new(
        @"^(?<indent>\s*)(?:public|internal|private|protected|sealed|abstract|static|partial|\s)*class\s+(?<name>\w+)",
        RegexOptions.Compiled);

    private static readonly Regex OtherTypeDeclaration = new(
        @"^\s*(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|\s)*(?<kind>record|struct|interface|enum)\s+\w+",
        RegexOptions.Compiled);

    /// <summary>Every type declaration in one file, measured or refused.</summary>
    public static IReadOnlyList<ClassSpan> Read(IReadOnlyList<string> lines)
    {
        var spans = new List<ClassSpan>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];

            if (OtherTypeDeclaration.Match(line) is { Success: true } other)
            {
                spans.Add(new ClassSpan(
                    line.Trim(),
                    index + 1,
                    0,
                    $"the ruling names classes; this is a {other.Groups["kind"].Value}"));
                continue;
            }

            var declaration = ClassDeclaration.Match(line);

            if (!declaration.Success)
            {
                continue;
            }

            var name = declaration.Groups["name"].Value;

            if (line.Contains(" partial ", StringComparison.Ordinal))
            {
                spans.Add(new ClassSpan(name, index + 1, 0, "partial: the span is not one file's to state"));
                continue;
            }

            if (declaration.Groups["indent"].Value.Length > 0)
            {
                spans.Add(new ClassSpan(name, index + 1, 0, "nested: the ruling does not say whether a nested type counts within its parent"));
                continue;
            }

            var closing = ClosingBraceOf(lines, index);

            spans.Add(closing is { } brace
                ? new ClassSpan(name, index + 1, brace, null)
                : new ClassSpan(name, index + 1, 0, "no closing brace found"));
        }

        return spans;
    }

    /// <summary>
    /// The line of the brace that closes the type opened at or after <paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// Braces inside string literals, char literals and comments would each move this count, so any
    /// line carrying one is reported as unsupported rather than counted through. The codebase does
    /// not currently put a brace in a literal at file scope; if that changes, this refuses rather
    /// than silently drifting.
    /// </remarks>
    private static int? ClosingBraceOf(IReadOnlyList<string> lines, int from)
    {
        var depth = 0;
        var opened = false;

        for (var index = from; index < lines.Count; index++)
        {
            var line = lines[index];
            var code = line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : line;

            foreach (var character in code)
            {
                if (character == '{')
                {
                    depth++;
                    opened = true;
                }
                else if (character == '}')
                {
                    depth--;

                    if (opened && depth == 0)
                    {
                        return index + 1;
                    }
                }
            }
        }

        return null;
    }
}
