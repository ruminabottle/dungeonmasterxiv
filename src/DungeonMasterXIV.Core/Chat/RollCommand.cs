using System;

namespace DungeonMasterXIV.Chat;

/// <summary>Recognises text that starts with the /roll command and extracts the dice expression after it.</summary>
public static class RollCommand
{
    public const string Token = "/roll";

    public static bool TryRead(string? text, out string expression)
    {
        expression = string.Empty;

        if (text is null)
        {
            return false;
        }

        var typed = text.TrimStart();

        if (!typed.StartsWith(Token, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = typed[Token.Length..];

        if (rest.Length > 0 && !char.IsWhiteSpace(rest[0]))
        {
            return false;
        }

        expression = rest.Trim();

        return true;
    }
}
