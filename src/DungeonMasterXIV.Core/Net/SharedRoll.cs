using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Rolls;

namespace DungeonMasterXIV.Net;

/// <summary>A roll as it travels to the session: what was typed, every die, the total and any notice.</summary>
public sealed record SharedRoll(
    string Expression,
    string? Label,
    IReadOnlyList<RolledDie> Dice,
    int Total,
    string? Notice)
{
    /// <summary>The most dice one shared roll carries, keeping its frame well under the relay's 64 KiB limit.</summary>
    public const int MaxDice = 500;

    public const int MaxLabelLength = 200;

    public const int MaxNoticeLength = 200;

    private const int SummaryExpressionLength = 100;

    private const int SummaryDice = 20;

    public static SharedRoll From(string expression, RollOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new SharedRoll(RollParser.BodyOf(expression).Trim(), outcome.Label, outcome.Dice, outcome.Total, outcome.Notice);
    }

    /// <summary>Why this roll cannot be shared, or null when it can.</summary>
    public string? RefusalToShare()
    {
        if (IsWithinBounds(RollLimits.Default))
        {
            return null;
        }

        if (Dice is not null && Dice.Count > MaxDice)
        {
            return $"That roll used {Dice.Count} dice. A roll shared with the session can carry up to {MaxDice}.";
        }

        return Label is not null && Label.Length > MaxLabelLength
            ? $"That roll's label is {Label.Length} characters. A roll shared with the session can carry a label of up to {MaxLabelLength}."
            : "That roll cannot be shared with the session.";
    }

    public bool IsWithinBounds(RollLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return Expression is not null
            && Expression.Length <= limits.MaxLength
            && (Label is null || Label.Length <= MaxLabelLength)
            && (Notice is null || Notice.Length <= MaxNoticeLength)
            && Dice is not null
            && Dice.Count <= MaxDice
            && Dice.All(die => die.Sides >= 1
                && die.Sides <= limits.MaxDieSize
                && Math.Abs(die.Value) <= limits.MaxDieSize);
    }

    /// <summary>A short plain-text form for the session log, such as "1d20+4 = 17 [13]".</summary>
    public string Summary()
    {
        var expression = Expression.Length > SummaryExpressionLength
            ? Expression[..SummaryCut()] + "…"
            : Expression;

        var dice = string.Join(", ", Dice.Take(SummaryDice).Select(die => die.Kept ? $"{die.Value}" : $"{die.Value}*"));
        var more = Dice.Count > SummaryDice ? ", …" : string.Empty;

        return Dice.Count == 0 ? $"{expression} = {Total}" : $"{expression} = {Total} [{dice}{more}]";
    }

    /// <summary>Where the summary cuts the expression, backing off so a surrogate pair is never split.</summary>
    private int SummaryCut() =>
        char.IsHighSurrogate(Expression[SummaryExpressionLength - 1]) ? SummaryExpressionLength - 1 : SummaryExpressionLength;
}
