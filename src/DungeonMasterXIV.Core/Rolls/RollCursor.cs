namespace DungeonMasterXIV.Rolls;

/// <summary>
/// A position in the expression text, with the small reads the grammar needs.
/// </summary>
/// <remarks>
/// <b>Separate from the parser so the grammar reads as grammar.</b> Character bookkeeping and
/// precedence rules are two different kinds of thinking, and interleaving them is how a parser
/// becomes the one method nobody will touch.
/// </remarks>
internal sealed class RollCursor(string text)
{
    private readonly string _text = text;

    /// <summary>Where the cursor is, used to report where a fault was found.</summary>
    public int Position { get; private set; }

    /// <summary>Whether every character has been consumed.</summary>
    public bool AtEnd
    {
        get
        {
            SkipWhitespace();
            return Position >= _text.Length;
        }
    }

    /// <summary>
    /// Whether the very next character is whitespace — asked WITHOUT skipping it (A-2.3c).
    /// </summary>
    /// <remarks>
    /// <b>Every other reader on this cursor skips whitespace first, and that is right for them.</b>
    /// <see cref="Peek"/>, <see cref="Take"/>, <see cref="TakeLetter"/> and <see cref="TryNumber"/>
    /// all call <c>SkipWhitespace</c>, because an operator or an operand may legitimately be
    /// separated from what precedes it — <c>2d6 + 3</c> is one expression.
    /// <b>A MODIFIER IS THE ONE THING THAT MAY NOT BE.</b> It is a suffix of its term, so
    /// <c>2d6 d20</c> must not read as <c>2d6</c> with a drop; the space ends the term.
    /// This exists so the modifier loop can ask that question, and nothing else should need it.
    /// </remarks>
    public bool NextIsSpace => Position < _text.Length && char.IsWhiteSpace(_text[Position]);

    /// <summary>The next character without consuming it, or null at the end.</summary>
    public char? Peek()
    {
        SkipWhitespace();
        return Position < _text.Length ? _text[Position] : null;
    }

    /// <summary>Consumes the next character if it is <paramref name="c"/>.</summary>
    public bool Take(char c)
    {
        if (Peek() != c)
        {
            return false;
        }

        Position++;
        return true;
    }

    /// <summary>
    /// Consumes the next character if it is <paramref name="c"/> in either case, for the letters the
    /// grammar uses — <c>d</c>, <c>k</c>, <c>x</c>, <c>r</c>.
    /// </summary>
    public bool TakeLetter(char c)
    {
        var next = Peek();
        return next is not null
            && char.ToLowerInvariant(next.Value) == char.ToLowerInvariant(c)
            && Take(next.Value);
    }

    /// <summary>
    /// Reads a run of digits. Returns false when the next character is not a digit, and when the
    /// number is too large to hold — which is a refusal, never a wrap.
    /// </summary>
    public bool TryNumber(out int value)
    {
        SkipWhitespace();
        value = 0;
        var start = Position;

        while (Position < _text.Length && char.IsAsciiDigit(_text[Position]))
        {
            // Guard BEFORE multiplying, so a long run of digits refuses rather than overflowing
            // into a plausible-looking small number.
            if (value > (int.MaxValue - 9) / 10)
            {
                return false;
            }

            value = (value * 10) + (_text[Position] - '0');
            Position++;
        }

        return Position > start;
    }

    private void SkipWhitespace()
    {
        while (Position < _text.Length && char.IsWhiteSpace(_text[Position]))
        {
            Position++;
        }
    }
}
