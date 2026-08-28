namespace DungeonMasterXIV.Sizes;

/// <summary>One type's line span, or the reason this tool will not put a number on it.</summary>
/// <param name="Name">The type as declared.</param>
/// <param name="DeclarationLine">1-based line of the declaration's first line.</param>
/// <param name="ClosingBraceLine">1-based line of its closing brace, or 0 when undetermined.</param>
/// <param name="Refusal">Why no number was produced, or null when <see cref="Lines"/> is meaningful.</param>
public sealed record ClassSpan(string Name, int DeclarationLine, int ClosingBraceLine, string? Refusal)
{
    /// <summary>The count under the ruled procedure: declaration line to closing brace, inclusive.</summary>
    public int Lines => Refusal is null ? ClosingBraceLine - DeclarationLine + 1 : 0;

    /// <summary>Whether this span carries a number at all.</summary>
    public bool IsMeasured => Refusal is null;
}
