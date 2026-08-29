using System.Collections.Generic;

namespace DungeonMasterXIV.Rolls;

/// <summary>
/// The state of one evaluation: the dice rolled so far, how much work has been spent, and the fault
/// that stopped it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The work budget is spent as dice are rolled, not predicted before.</b> <see cref="RollLimits"/>
/// explains why: how many times a die explodes is not knowable in advance, so an expression whose
/// SHAPE passes every bound can still cost unboundedly much. Counting at the point of rolling is the
/// only place that catches it.
/// </para>
/// <para>
/// <b>A fault stops the walk rather than unwinding it.</b> Once <see cref="Fault"/> is set every
/// caller returns null upward, so no further dice are rolled — a refusal must not keep spending the
/// budget it was raised to protect.
/// </para>
/// </remarks>
internal sealed class RollEvaluation(IDieRoller roller, RollLimits limits)
{
    private readonly List<RolledDie> _dice = [];
    private int _work;

    /// <summary>Every die rolled, in order, kept and dropped alike (A-2.1a).</summary>
    public IReadOnlyList<RolledDie> Dice => _dice;

    /// <summary>The fault that stopped evaluation, or <see cref="RollFault.None"/>.</summary>
    public RollFault Fault { get; private set; }

    /// <summary>What went wrong, in words, or null.</summary>
    public string? Message { get; private set; }

    /// <summary>The bounds this evaluation is held to.</summary>
    public RollLimits Limits { get; } = limits;

    /// <summary>Whether a fault has already stopped this evaluation.</summary>
    public bool Stopped => Fault is not RollFault.None;

    /// <summary>
    /// Rolls one die, spending a unit of the work budget. Returns null once the budget is gone,
    /// having recorded <see cref="RollFault.TooMuchWork"/>.
    /// </summary>
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

    /// <summary>Records a die in the result, kept or not.</summary>
    public void Record(int sides, int value, bool kept) => _dice.Add(new RolledDie(sides, value, kept));

    /// <summary>Replaces the kept flag of the die at <paramref name="index"/>, for keep/drop.</summary>
    public void SetKept(int index, bool kept) => _dice[index] = _dice[index] with { Kept = kept };

    /// <summary>How many dice have been recorded, so a term can find the ones it added.</summary>
    public int RecordedCount => _dice.Count;

    /// <summary>Stops the evaluation with a named fault.</summary>
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
