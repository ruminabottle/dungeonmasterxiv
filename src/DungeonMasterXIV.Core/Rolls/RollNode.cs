namespace DungeonMasterXIV.Rolls;

/// <summary>An arithmetic operator between two sub-expressions.</summary>
public enum RollOperator
{
    /// <summary>Addition.</summary>
    Add = 0,

    /// <summary>Subtraction.</summary>
    Subtract,

    /// <summary>Multiplication.</summary>
    Multiply,

    /// <summary>Integer division. Division by zero is refused, not infinite.</summary>
    Divide,
}

/// <summary>
/// A parsed expression. <b>The parse is separate from the evaluation on purpose</b> — every
/// structural fault (malformed, unbalanced, too deeply nested) is decided here, before a single die
/// is rolled, so a refusal costs nothing and cannot be reached halfway through a roll.
/// </summary>
public abstract record RollNode;

/// <summary>A literal number — the <c>2</c> in <c>1d20+2</c>.</summary>
/// <param name="Value">The number as written.</param>
public sealed record NumberNode(int Value) : RollNode;

/// <summary>
/// A dice term: how many, of what size, with what modifiers. <c>d20</c> parses to a count of 1,
/// which is R-2.1's rule that <i>d20 means 1d20</i>.
/// </summary>
/// <param name="Count">How many dice. Bounded by <see cref="RollLimits.MaxDicePerTerm"/>.</param>
/// <param name="Sides">Faces per die. Bounded by <see cref="RollLimits.MaxDieSize"/>.</param>
/// <param name="Modifiers">Keep/drop, exploding, rerolls, success counting.</param>
public sealed record DiceNode(int Count, int Sides, DiceModifiers Modifiers) : RollNode;

/// <summary>Two sub-expressions and an operator between them.</summary>
/// <param name="Operator">Which arithmetic.</param>
/// <param name="Left">Left operand.</param>
/// <param name="Right">Right operand.</param>
public sealed record BinaryNode(RollOperator Operator, RollNode Left, RollNode Right) : RollNode;

/// <summary>
/// A sub-expression with the sign flipped — the leading minus in <c>-1d4</c>.
/// </summary>
/// <param name="Operand">What is negated.</param>
public sealed record NegateNode(RollNode Operand) : RollNode;
