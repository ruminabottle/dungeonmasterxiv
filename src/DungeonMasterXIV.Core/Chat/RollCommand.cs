using System;

namespace DungeonMasterXIV.Chat;

/// <summary>
/// Recognises the roll command in text a person typed (R-2.1, A-2.33a).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE TOKEN IS <c>/roll</c> AND IT IS RULED, NOT CHOSEN HERE.</b> A-2.33b states the
/// requirement as <i>the token a tabletop player tries FIRST that does not SHADOW A GAME
/// COMMAND</i>, and records <c>/roll</c> as what satisfies it today. <b>A PR may not change the
/// token by editing the criterion</b> — if the condition and the word ever conflict, R-2.18 governs
/// and A-2.33b is amended.
/// </para>
/// <para>
/// <b><c>/r</c> IS CLAIMED IN NO WAY, AND THAT IS A SEPARATE HALF THAT FAILS SEPARATELY.</b>
/// A-2.33a: not a handler, not an alias table entry, not a completion suggestion. <b><c>/r</c> is
/// REPLY in FFXIV</b>, and shadowing it sends a player's tell somewhere else, possibly in front of
/// the table. So the match is the WHOLE token <c>/roll</c> delimited by whitespace or end of input —
/// <b>a prefix match would claim <c>/r</c> by construction</b>, and <c>/rolling</c> with it.
/// </para>
/// <para>
/// <b>WHY THIS IS READ FROM THE PRODUCT'S OWN INPUT RATHER THAN REGISTERED AS A GAME-WIDE
/// COMMAND.</b> A-2.33c requires that <c>/roll</c> being free in FFXIV is VERIFIED FIRST if it is
/// registered game-wide, and marks that check <b>in-game, human</b> — the Product Owner named it
/// unverified rather than assuming it. The same row states the boundary in terms: the criterion is
/// <i>"VACUOUS AND CORRECTLY SO WHILE THE TOKEN IS TYPED ONLY INTO THE PRODUCT'S OWN INPUT — the
/// trigger is a game-wide registration."</i> <b>Reading it here keeps A-2.33c correctly vacuous;
/// registering it would owe a check nobody on this team can run.</b>
/// </para>
/// </remarks>
public static class RollCommand
{
    /// <summary>The ruled token (A-2.33b). Not a preference, and not this file's to change.</summary>
    public const string Token = "/roll";

    /// <summary>
    /// Reads <paramref name="text"/> as a roll command, yielding the expression after the token.
    /// </summary>
    /// <remarks>
    /// <b>The expression is handed on UNPARSED and UNTRIMMED of meaning.</b> What is a valid roll is
    /// <c>RollEvaluator</c>'s (DMXENG-84, shipped), and deciding any part of it here would put the
    /// grammar in two places — which is the drift a second reader of one input always becomes.
    /// <b>An empty expression is still a recognised COMMAND</b>: refusing it here would answer a
    /// grammar question, and the evaluator already names its own faults.
    /// </remarks>
    /// <param name="text">What the person typed.</param>
    /// <param name="expression">The text after the token, or empty.</param>
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

        // Whitespace or nothing after the token, never a longer word. Without this, `/rolling` is a
        // roll -- and the same construction that admits it is the one that would admit `/r`.
        if (rest.Length > 0 && !char.IsWhiteSpace(rest[0]))
        {
            return false;
        }

        expression = rest.Trim();

        return true;
    }
}
