namespace DungeonMasterXIV.Rolls;

/// <summary>The result of parsing a dice expression or part of one: a node and label, or a fault and message.</summary>
internal sealed record RollParse(RollNode? Node, string? Label, RollFault Fault, string? Message)
{
    public static RollParse Parsed(RollNode node, string? label) => new(node, label, RollFault.None, null);

    public static RollParse Refused(RollFault fault, string message) => new(null, null, fault, message);
}
