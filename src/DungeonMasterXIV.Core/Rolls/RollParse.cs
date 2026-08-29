namespace DungeonMasterXIV.Rolls;

/// <summary>
/// The outcome of reading an expression: a tree, or a fault naming what was wrong.
/// </summary>
/// <param name="Node">The parsed expression, or null when <paramref name="Fault"/> is set.</param>
/// <param name="Label">The free-text label the expression carried, or null. Never interpreted (D-4).</param>
/// <param name="Fault">Which structural fault, or <see cref="RollFault.None"/>.</param>
/// <param name="Message">A human-readable statement of the fault, or null.</param>
/// <remarks>
/// <b>Structural faults are decided here, before any die is rolled.</b> Malformed notation,
/// unbalanced parentheses, a bad number, an over-deep nest and the two shape bounds are all
/// knowable from the text alone — so they cost nothing, and a refusal can never arrive halfway
/// through a roll with dice already spent.
/// </remarks>
internal sealed record RollParse(RollNode? Node, string? Label, RollFault Fault, string? Message)
{
    /// <summary>A successful parse.</summary>
    public static RollParse Parsed(RollNode node, string? label) => new(node, label, RollFault.None, null);

    /// <summary>A refusal naming its fault.</summary>
    public static RollParse Refused(RollFault fault, string message) => new(null, null, fault, message);
}
