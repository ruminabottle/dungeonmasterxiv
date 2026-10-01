namespace DungeonMasterXIV.Rolls;

public enum ComparisonOperator
{
    Equal = 0,

    Greater,

    Less,

    AtLeast,

    AtMost,
}

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
