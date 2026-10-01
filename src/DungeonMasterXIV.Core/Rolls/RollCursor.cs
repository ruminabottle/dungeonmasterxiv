namespace DungeonMasterXIV.Rolls;

/// <summary>Steps through a dice expression's text, skipping whitespace, taking characters and reading numbers.</summary>
internal sealed class RollCursor(string text)
{
    private readonly string _text = text;

    public int Position { get; private set; }

    public bool AtEnd
    {
        get
        {
            SkipWhitespace();
            return Position >= _text.Length;
        }
    }

    public bool AtWhitespace => Position < _text.Length && char.IsWhiteSpace(_text[Position]);

    public char? Peek()
    {
        SkipWhitespace();
        return Position < _text.Length ? _text[Position] : null;
    }

    public bool Take(char c)
    {
        if (Peek() != c)
        {
            return false;
        }

        Position++;
        return true;
    }

    public bool TakeLetter(char c)
    {
        var next = Peek();
        return next is not null
            && char.ToLowerInvariant(next.Value) == char.ToLowerInvariant(c)
            && Take(next.Value);
    }

    public bool TryNumber(out int value)
    {
        SkipWhitespace();
        value = 0;
        var start = Position;

        while (Position < _text.Length && char.IsAsciiDigit(_text[Position]))
        {
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
