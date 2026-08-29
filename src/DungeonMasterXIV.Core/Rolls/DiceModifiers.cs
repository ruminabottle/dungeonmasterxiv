namespace DungeonMasterXIV.Rolls;

/// <summary>
/// What a dice term does beyond rolling: keep/drop, exploding, rerolls, and success counting.
/// </summary>
/// <remarks>
/// <para>
/// <b>One type for all of them because they compose on a single term.</b> <c>4d6kh3</c>,
/// <c>10d10x&gt;9</c> and <c>6d6r1&gt;4</c> are all one dice term with different modifiers, and
/// modelling each as its own wrapper node would put the ORDER of wrapping into the AST where the
/// grammar has no order to express.
/// </para>
/// <para>
/// <b>Every field is null when absent</b> rather than a sentinel, so "no keep" and "keep zero" stay
/// distinguishable — the second is a legal thing to write and means an empty result, not an absent
/// modifier.
/// </para>
/// </remarks>
public sealed record DiceModifiers
{
    /// <summary>Nothing beyond rolling the dice.</summary>
    public static DiceModifiers None { get; } = new();

    /// <summary>Keep the N highest dice, dropping the rest — <c>kh3</c>.</summary>
    public int? KeepHighest { get; init; }

    /// <summary>Keep the N lowest — <c>kl1</c>.</summary>
    public int? KeepLowest { get; init; }

    /// <summary>Drop the N highest — <c>dh1</c>.</summary>
    public int? DropHighest { get; init; }

    /// <summary>Drop the N lowest — <c>dl1</c>, the common "4d6 drop lowest".</summary>
    public int? DropLowest { get; init; }

    /// <summary>
    /// Reroll dice matching this test, once each — <c>r1</c> or <c>r&lt;3</c>. The discarded die is
    /// kept in the result as not-kept, so the reroll is visible rather than inferred.
    /// </summary>
    public RollComparison? Reroll { get; init; }

    /// <summary>
    /// Roll another die whenever one matches this test — <c>x</c> (on the maximum face) or
    /// <c>x&gt;8</c>. Bounded by <see cref="RollLimits.MaxWork"/>, which is the bound that exists
    /// because this modifier makes a shape-legal expression cost unboundedly much.
    /// </summary>
    public RollComparison? Explode { get; init; }

    /// <summary>
    /// Count how many dice match, instead of summing them — <c>4d6&gt;3</c>.
    /// </summary>
    /// <remarks>
    /// <b>A count, never a verdict.</b> See <see cref="RollComparison"/> — the threshold is a number
    /// the user typed, and the result is an integer, not a decision about whether the roll succeeded.
    /// </remarks>
    public RollComparison? CountSuccesses { get; init; }

    /// <summary>Whether any modifier is set, so the evaluator can take the plain path.</summary>
    public bool Any =>
        KeepHighest is not null || KeepLowest is not null || DropHighest is not null
        || DropLowest is not null || Reroll is not null || Explode is not null
        || CountSuccesses is not null;
}
