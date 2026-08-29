namespace DungeonMasterXIV.Rolls;

/// <summary>
/// What an expression may not exceed. <b>A roll expression is untrusted input</b> (R-2.1a) — it
/// arrives in a peer's message, and a peer may be hostile or merely careless.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE VALUES HERE ARE ENGINEERING'S; THAT BOUNDS EXIST IS NOT.</b> R-2.1a says so in those
/// terms. So these numbers may be tuned by anyone with a reason, and the <i>presence</i> of each
/// bound may not be removed without going back to the product.
/// </para>
/// <para>
/// <b>Bounded BEFORE it is reachable, not after.</b> The requirement is explicit that this is not a
/// later hardening pass, and the reason is a timing one: <i>the input is untrusted the day it is
/// wired, not the day someone remembers</i>. An evaluator that is bounded only once it has a caller
/// has already shipped unbounded.
/// </para>
/// <para>
/// <b>A refusal is the required outcome — a freeze is a FAILED requirement, not a slow one.</b>
/// R-2.1a puts a hang and an out-of-memory in the same category as a wrong answer, and A-2.3a fails
/// a run that eventually recovers. That is why <see cref="MaxWork"/> exists alongside the shape
/// bounds: <c>20d6</c> is small, and <c>20d6</c> exploding without a ceiling is not.
/// </para>
/// </remarks>
public sealed record RollLimits
{
    /// <summary>The bounds applied when a caller does not choose its own.</summary>
    public static RollLimits Default { get; } = new();

    /// <summary>
    /// The most dice a single term may ask for. <c>999999d6</c> is refused by this one.
    /// </summary>
    public int MaxDicePerTerm { get; init; } = 1000;

    /// <summary>
    /// The largest die face. Generous, because unusual dice are legitimate — <c>d100</c> is
    /// ordinary and <c>d1000</c> is somebody's table rule, while <c>d999999</c> is not a die.
    /// </summary>
    public int MaxDieSize { get; init; } = 10000;

    /// <summary>
    /// How deeply parentheses and nested terms may be stacked. Bounds the PARSER, so that
    /// <c>((((…))))</c> is refused while being read rather than after — a deep expression must not
    /// be able to exhaust the stack before anything measures it.
    /// </summary>
    public int MaxNestingDepth { get; init; } = 32;

    /// <summary>
    /// The total number of dice the whole expression may roll, across every term and every
    /// explosion and reroll.
    /// </summary>
    /// <remarks>
    /// <b>This is the bound the other three cannot express.</b> Each of them limits the SHAPE of the
    /// expression; this limits what evaluating it COSTS. An expression can satisfy every shape bound
    /// and still be unbounded work — exploding dice on a large pool is the ordinary example, and it
    /// is not hostile, merely expensive. Checked as the dice are rolled rather than predicted, since
    /// how many times a die explodes is not knowable in advance.
    /// </remarks>
    public int MaxWork { get; init; } = 20000;

    /// <summary>
    /// The longest expression text accepted, so the lexer has a bound of its own and a megabyte of
    /// digits is refused before it becomes tokens.
    /// </summary>
    public int MaxLength { get; init; } = 2000;
}
