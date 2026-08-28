using System.Text.RegularExpressions;

namespace DungeonMasterXIV.Sizes;

/// <summary>
/// Counts type spans under the procedure ruled in <c>engineering-standards.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The procedure, cited rather than restated</b> — see "## HOW TO COUNT A CLASS" and its
/// subsection "### THE SHAPES A REAL FILE HAS": first line of the TYPE declaration to its closing
/// brace, INCLUSIVE, nothing excluded. Attributes and doc above the declaration are outside it.
/// </para>
/// <para>
/// <b>Records, structs, interfaces and enums count as classes</b>, because the limit measures what a
/// reader must hold at once and a 300-line record imposes what a 300-line class does. <b>A nested
/// type counts twice</b> — inside its parent's span and again as its own type against its own limit.
/// <b>Generic constraints between the declaration and the brace are inside</b>, which falls out of
/// the procedure rather than being a new rule.
/// </para>
/// <para>
/// <b>A partial class is the SUM of its parts, so this refuses it and names it.</b> Summing needs
/// every part and a file-at-a-time reader sees one. Reporting the part it can see would not be an
/// underestimate — it would be a number that looks exactly like an answer, arrives under the limit,
/// and is wrong in the reassuring direction. That is the defect family this tool exists downstream
/// of; shipping it here would be the joke writing itself.
/// </para>
/// <para>
/// <b>Anything this does not cover is refused and named, never picked.</b> A refusal costs a
/// message; a silent pick costs a convention.
/// </para>
/// </remarks>
public static class ClassSpanReader
{
    private const string Modifiers = @"(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|ref|file|new|\s)*";

    private static readonly Regex TypeDeclaration = new(
        @"^(?<indent>\s*)" + Modifiers + @"\b(?<kind>class|record\s+struct|record\s+class|record|struct|interface|enum)\s+(?<name>\w+)",
        RegexOptions.Compiled);

    /// <summary>Every type declaration in one file, measured or refused by name.</summary>
    public static IReadOnlyList<ClassSpan> Read(IReadOnlyList<string> lines)
    {
        var spans = new List<ClassSpan>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];

            // A declaration quoted in prose is not a declaration. Doc and comment lines are skipped
            // outright rather than parsed, which is also why the span never starts on one.
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            var declaration = TypeDeclaration.Match(line);

            if (!declaration.Success)
            {
                continue;
            }

            var name = declaration.Groups["name"].Value;

            // RULED: a partial type is the sum of its parts, and this reader sees one file. It must
            // refuse and say which type -- never report the part it can see.
            if (Regex.IsMatch(line, @"\bpartial\b"))
            {
                spans.Add(new ClassSpan(
                    name,
                    index + 1,
                    0,
                    "partial: counted as the SUM of its parts, and this reads one file — measure every part together"));
                continue;
            }

            var end = EndOfDeclaration(lines, index);

            spans.Add(end is { } last
                ? new ClassSpan(name, index + 1, last, null)
                : new ClassSpan(name, index + 1, 0, "no closing brace or terminator found before end of file"));
        }

        return spans;
    }

    /// <summary>
    /// The last line of the type opened at <paramref name="from"/>: its closing brace, or the
    /// semicolon that ends a body-less declaration.
    /// </summary>
    /// <remarks>
    /// <b>The body-less case is real and would otherwise run past the end of the type.</b>
    /// <c>public readonly record struct RosterEntry(string PeerCode, ...);</c> has no braces at all,
    /// so a brace scanner would keep going and close on the NEXT type's brace — producing a number
    /// that looks like an answer for a span that never existed.
    /// </remarks>
    private static int? EndOfDeclaration(IReadOnlyList<string> lines, int from)
    {
        var depth = 0;
        var opened = false;

        for (var index = from; index < lines.Count; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();
            var code = trimmed.StartsWith("//", StringComparison.Ordinal) ? string.Empty : line;

            foreach (var character in code)
            {
                switch (character)
                {
                    case '{':
                        depth++;
                        opened = true;
                        break;

                    case '}':
                        depth--;

                        if (opened && depth == 0)
                        {
                            return index + 1;
                        }

                        break;

                    case ';' when !opened:
                        // A declaration that ended without ever opening a body.
                        return index + 1;
                }
            }
        }

        return null;
    }
}
