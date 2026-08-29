namespace DungeonMasterXIV.Rolls;

/// <summary>How a die's face is compared against a number the user typed.</summary>
public enum ComparisonOperator
{
    /// <summary>Equal to.</summary>
    Equal = 0,

    /// <summary>Greater than.</summary>
    Greater,

    /// <summary>Less than.</summary>
    Less,

    /// <summary>Greater than or equal to.</summary>
    AtLeast,

    /// <summary>Less than or equal to.</summary>
    AtMost,
}

/// <summary>
/// A test applied to a single die: an operator and <b>a number the user typed</b>.
/// </summary>
/// <param name="Operator">Which comparison.</param>
/// <param name="Value">The number from the expression. Never supplied by this assembly.</param>
/// <remarks>
/// <para>
/// <b>THIS TYPE IS EXACTLY WHERE CONSTRAINT 3 LIVES, SO THE LINE IS WRITTEN ON IT.</b> R-2.1 draws
/// it in these terms: <i>counting successes against a number the user typed is arithmetic on dice
/// and is in scope; knowing what number to compare against is rules content and is not.</i>
/// </para>
/// <para>
/// So <see cref="Value"/> comes from the expression text and from nowhere else. <b>Nothing in this
/// assembly may supply a default, infer one, or look one up</b> — the moment a comparison can get
/// its threshold from anywhere but the typed characters, the plugin has begun to know what a roll
/// MEANS, which D-4 forbids and which the human has deferred.
/// </para>
/// <para>
/// <b>And a comparison yields a COUNT, never a verdict.</b> <c>4d6&gt;3</c> is "how many dice beat
/// three", an integer like any other. It is not "did this succeed" — the evaluator is where
/// success/failure resolution would first become expressible, and it must not become expressible
/// here.
/// </para>
/// </remarks>
public readonly record struct RollComparison(ComparisonOperator Operator, int Value)
{
    /// <summary>Whether <paramref name="face"/> satisfies this comparison.</summary>
    public bool Matches(int face) => Operator switch
    {
        ComparisonOperator.Equal => face == Value,
        ComparisonOperator.Greater => face > Value,
        ComparisonOperator.Less => face < Value,
        ComparisonOperator.AtLeast => face >= Value,
        _ => face <= Value,
    };
}
