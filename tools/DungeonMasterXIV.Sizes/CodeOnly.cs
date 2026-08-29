using System.Text;

namespace DungeonMasterXIV.Sizes;

/// <summary>
/// One line of C# with its comments and literals blanked out, so a brace scan sees only braces.
/// </summary>
/// <remarks>
/// <para>
/// <b>A BRACE INSIDE A LITERAL IS NOT A BRACE, AND THE RULED PROCEDURE ALREADY SAYS SO</b> — the
/// class span runs to <i>its closing brace</i>, and <c>'}'</c> in a char literal is not one. This is
/// a lexing bug in a reader that misread the rule, not a change to what the rule counts. The
/// Deployment Manager ruled it on exactly that ground.
/// </para>
/// <para>
/// <b>Both arms are real and one of them lies.</b> An unmatched <c>'{'</c> makes the scan run off
/// the end and the type is REFUSED — honest, and visible in the census. An unmatched <c>'}'</c>
/// closes the type EARLY and reports <b>a falsely short span</b>: a class that is over a limit can
/// report under it, in the instrument cited at every size gate.
/// </para>
/// <para>
/// <b>Seven types in this tree were refused, not one.</b> Char literals accounted for two; the other
/// five were braces in ordinary STRING literals — JSON fixtures like <c>"{ not json"</c> and
/// <c>$"{{\"Version\":…}}"</c>. Anyone estimating the blast radius from char literals alone
/// undercounts it by a factor of three.
/// </para>
/// <para>
/// <b>Blanking rather than deleting.</b> Literal content is replaced with spaces so column positions
/// survive; nothing downstream uses them today and a reader that quietly shortens lines is a trap
/// for whatever does next.
/// </para>
/// <para>
/// <b>What this does NOT do, stated rather than discovered later.</b> It carries no state between
/// lines, so a block comment or a raw string spanning several lines is handled only from the line
/// it opens on — see <see cref="Of(string, ref bool)"/>, which threads the block-comment state that
/// actually occurs here. A raw string containing an unbalanced brace and spanning lines would still
/// fool it. None exists in this tree; the limit is named so the next person meets it in a comment
/// rather than in a wrong number.
/// </para>
/// </remarks>
public static class CodeOnly
{
    /// <summary>The line with comment and literal content blanked out.</summary>
    /// <param name="line">One line of source.</param>
    /// <param name="inBlockComment">
    /// Whether the line begins inside a <c>/* … */</c>, updated on the way out. Threaded because a
    /// block comment is the one multi-line construct in this tree that can hide a brace.
    /// </param>
    public static string Of(string line, ref bool inBlockComment)
    {
        var blanked = new StringBuilder(line.Length);
        var index = 0;

        while (index < line.Length)
        {
            if (inBlockComment)
            {
                if (Is(line, index, "*/"))
                {
                    inBlockComment = false;
                    blanked.Append("  ");
                    index += 2;
                    continue;
                }

                blanked.Append(' ');
                index++;
                continue;
            }

            if (Is(line, index, "//"))
            {
                // The rest of the line is commentary. Blanked wholesale, which is what the previous
                // reader did only for lines that START with a comment -- a trailing "// }" was
                // counted as a brace.
                blanked.Append(new string(' ', line.Length - index));
                break;
            }

            if (Is(line, index, "/*"))
            {
                inBlockComment = true;
                blanked.Append("  ");
                index += 2;
                continue;
            }

            if (line[index] is '"' or '\'')
            {
                index = SkipLiteral(line, index, blanked);
                continue;
            }

            blanked.Append(line[index]);
            index++;
        }

        return blanked.ToString();
    }

    private static bool Is(string line, int index, string token) =>
        index + token.Length <= line.Length && line.AsSpan(index, token.Length).SequenceEqual(token);

    /// <summary>
    /// Blanks the literal starting at <paramref name="from"/> and returns the index after it.
    /// </summary>
    /// <remarks>
    /// <b>An interpolated string is skipped WHOLE, holes included, and that is safe for a brace
    /// count rather than merely convenient.</b> Whatever a hole contains, its own braces are a
    /// matched pair — so skipping the entire literal removes as many opens as closes. Counting into
    /// the holes would be more faithful and would buy nothing this reader needs.
    /// </remarks>
    private static int SkipLiteral(string line, int from, StringBuilder blanked)
    {
        var quote = line[from];
        var verbatim = from > 0 && (line[from - 1] == '@' || (from > 1 && line[from - 2] == '@'));

        blanked.Append(quote);
        var index = from + 1;

        while (index < line.Length)
        {
            if (!verbatim && line[index] == '\\' && index + 1 < line.Length)
            {
                // An escape consumes the next character, so a \" does not end the literal and a
                // '\\' is one backslash rather than the start of another escape.
                blanked.Append("  ");
                index += 2;
                continue;
            }

            if (line[index] == quote)
            {
                if (verbatim && index + 1 < line.Length && line[index + 1] == quote)
                {
                    // "" inside a verbatim string is one escaped quote, not the end.
                    blanked.Append("  ");
                    index += 2;
                    continue;
                }

                blanked.Append(quote);
                return index + 1;
            }

            blanked.Append(' ');
            index++;
        }

        // An unterminated literal on this line. Everything to the end has been blanked, which is the
        // safe direction: it removes braces rather than inventing them.
        return index;
    }
}
