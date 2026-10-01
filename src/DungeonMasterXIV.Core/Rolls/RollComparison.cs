namespace DungeonMasterXIV.Rolls;

/// <summary>How a die face is compared with a number: equal, greater, less, at least, or at most.</summary>
public enum ComparisonOperator
{
    Equal = 0,

    Greater,

    Less,

    AtLeast,

    AtMost,
}

/// <summary>A test a die face can match, such as at least 5, used for rerolls, explosions and successes.</summary>
public readonly record struct RollComparison(ComparisonOperator Operator, int Value)
{
    public bool Matches(int face) => Operator switch
    {
        ComparisonOperator.Equal => face == Value,
        ComparisonOperator.Greater => face > Value,
        ComparisonOperator.Less => face < Value,
        ComparisonOperator.AtLeast => face >= Value,
        _ => face <= Value,
    };
}
