using System.Collections.Generic;

namespace DungeonMasterXIV.Rolls;

internal sealed class RollEvaluation(IDieRoller roller, RollLimits limits)
{
    private readonly List<RolledDie> _dice = [];
    private int _work;

    public IReadOnlyList<RolledDie> Dice => _dice;

    public RollFault Fault { get; private set; }

    public string? Message { get; private set; }

    public RollLimits Limits { get; } = limits;

    public bool Stopped => Fault is not RollFault.None;

    public int? RollOne(int sides)
    {
        if (Stopped)
        {
            return null;
        }

        if (++_work > Limits.MaxWork)
        {
            Refuse(
                RollFault.TooMuchWork,
                $"Evaluating this would roll more than {Limits.MaxWork} dice.");
            return null;
        }

        return roller.Roll(sides);
    }

    public void Record(int sides, int value, bool kept) => _dice.Add(new RolledDie(sides, value, kept));

    public void SetKept(int index, bool kept) => _dice[index] = _dice[index] with { Kept = kept };

    public int RecordedCount => _dice.Count;

    public void Refuse(RollFault fault, string message)
    {
        if (Stopped)
        {
            return;
        }

        Fault = fault;
        Message = message;
    }
}
