namespace DungeonMasterXIV.Rolls;

public enum RollOperator
{
    Add = 0,

    Subtract,

    Multiply,

    Divide,
}

public abstract record RollNode;

public sealed record NumberNode(int Value) : RollNode;

public sealed record DiceNode(int Count, int Sides, DiceModifiers Modifiers) : RollNode;

public sealed record BinaryNode(RollOperator Operator, RollNode Left, RollNode Right) : RollNode;

public sealed record NegateNode(RollNode Operand) : RollNode;
