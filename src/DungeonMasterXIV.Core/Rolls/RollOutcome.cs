using System.Collections.Generic;

namespace DungeonMasterXIV.Rolls;

/// <summary>
/// What evaluating an expression produced: either a total with the dice behind it, or a refusal
/// naming what was wrong. <b>Never both, and never neither.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>A refusal is a RESULT, not an exception.</b> R-2.1a makes a refusal the required outcome for
/// untrusted input, so it is modelled as a value the caller must look at rather than a throw it
/// might not catch. An evaluator that threw on <c>999999d999999</c> would satisfy "did not freeze"
/// and still hand a caller something it can drop on the floor.
/// </para>
/// <para>
/// <b><see cref="Dice"/> is populated on success and is the point of the type</b> (A-2.1a). The
/// total alone is unfalsifiable: any wrong roller also produces a number. The dice are what let a
/// test — or a human reading a log — check the total against something that is not the roller.
/// </para>
/// <para>
/// <b>The label is carried and never read.</b> D-4 and R-2.1: the plugin stores and displays a
/// label and never interprets it. It is a string here and nothing in this assembly branches on it.
/// </para>
/// </remarks>
public sealed record RollOutcome
{
    private RollOutcome()
    {
    }

    /// <summary>Whether the expression evaluated. False means <see cref="Fault"/> says why.</summary>
    public bool Evaluated { get; private init; }

    /// <summary>The total, when <see cref="Evaluated"/>. Zero otherwise, and meaningless.</summary>
    public int Total { get; private init; }

    /// <summary>
    /// Every die rolled, in the order rolled, including those dropped or rerolled — see
    /// <see cref="RolledDie.Kept"/>. Empty for an expression with no dice, such as <c>2+2</c>.
    /// </summary>
    public IReadOnlyList<RolledDie> Dice { get; private init; } = [];

    /// <summary>The free-text label the expression carried, or null. Never interpreted (D-4).</summary>
    public string? Label { get; private init; }

    /// <summary>Which fault caused the refusal, or <see cref="RollFault.None"/> on success.</summary>
    public RollFault Fault { get; private init; }

    /// <summary>A human-readable statement of what was wrong, or null on success.</summary>
    public string? Message { get; private init; }

    /// <summary>A successful evaluation and the dice that produced it.</summary>
    public static RollOutcome Rolled(int total, IReadOnlyList<RolledDie> dice, string? label = null) =>
        new() { Evaluated = true, Total = total, Dice = dice, Label = label };

    /// <summary>A refusal naming its fault (R-2.1, R-2.1a).</summary>
    public static RollOutcome Refused(RollFault fault, string message) =>
        new() { Evaluated = false, Fault = fault, Message = message };
}
