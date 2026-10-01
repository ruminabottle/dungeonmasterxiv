using System.Collections.Generic;

namespace DungeonMasterXIV.Rolls;

/// <summary>The result of a roll: the total, dice, label and any notice, or the fault and message if refused.</summary>
public sealed record RollOutcome
{
    private RollOutcome()
    {
    }

    public bool Evaluated { get; private init; }

    public int Total { get; private init; }

    public IReadOnlyList<RolledDie> Dice { get; private init; } = [];

    public string? Label { get; private init; }

    public RollFault Fault { get; private init; }

    public string? Message { get; private init; }

    public string? Notice { get; private init; }

    public static RollOutcome Rolled(int total, IReadOnlyList<RolledDie> dice, string? label = null) =>
        new()
        {
            Evaluated = true,
            Total = total,
            Dice = dice,
            Label = label,
            Notice = RollSurvival.NoticeFor(dice),
        };

    public static RollOutcome Refused(RollFault fault, string message) =>
        new() { Evaluated = false, Fault = fault, Message = message };
}
