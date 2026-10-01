namespace DungeonMasterXIV.Rolls;

/// <summary>The arithmetic operators a dice expression supports: add, subtract, multiply and divide.</summary>
public enum RollOperator
{
    Add = 0,

    Subtract,

    Multiply,

    Divide,
}

/// <summary>A node in a parsed dice expression.</summary>
public abstract record RollNode;

/// <summary>A plain number in a parsed dice expression.</summary>
public sealed record NumberNode(int Value) : RollNode;

/// <summary>A dice term in a parsed dice expression: how many dice, how many sides, and their modifiers.</summary>
public sealed record DiceNode(int Count, int Sides, DiceModifiers Modifiers) : RollNode;

/// <summary>An arithmetic operation on two parts of a parsed dice expression.</summary>
public sealed record BinaryNode(RollOperator Operator, RollNode Left, RollNode Right) : RollNode;

/// <summary>A negated part of a parsed dice expression.</summary>
public sealed record NegateNode(RollNode Operand) : RollNode;
