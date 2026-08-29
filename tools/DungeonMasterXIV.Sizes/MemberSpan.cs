namespace DungeonMasterXIV.Sizes;

/// <summary>
/// One member measured against the method, parameter and nesting rows — or the reason it was not.
/// </summary>
/// <remarks>
/// <b>Three rows on one record because they are three readings of one member</b>, taken in one pass.
/// Splitting them into three result types would let a future caller report one row and omit the
/// others, which is the failure this whole ticket is downstream of.
/// </remarks>
/// <param name="Name">The member as a reader would name it, e.g. <c>HostRunner(...)</c>.</param>
/// <param name="Line">1-based line of the declaration's first line.</param>
/// <param name="Lines">
/// Declaration line to end, inclusive — the same procedure the class row uses, applied to a member.
/// <b>An expression-bodied member is a method for this row</b>, ruled by the Deployment Manager: it
/// will almost never bind, and the case where it does is exactly the one worth catching.
/// </param>
/// <param name="Parameters">
/// How many the member declares. <b>A primary constructor's list counts</b>, ruled: it is the type's
/// construction surface and identical in effect to a declared constructor, and ruling otherwise
/// would make the row evadable by syntax choice.
/// </param>
/// <param name="Depth">
/// The deepest nesting of control flow in the body. <b>A lambda body does not add depth by itself</b>,
/// ruled: control flow inside one counts from the lambda's own baseline, because the row exists to
/// stop conditional pyramids and a lambda is usually what flattens one.
/// <para>
/// <b>The Deployment Manager holds that third ruling loosely and said so.</b> It is a judgement
/// about what the row is FOR rather than a fact about counting. If it starts hiding real nesting,
/// the case goes back to them and they will reverse it.
/// </para>
/// </param>
/// <param name="Refusal">Why no numbers were produced, or null when they are meaningful.</param>
public sealed record MemberSpan(
    string Name,
    int Line,
    int Lines,
    int Parameters,
    int Depth,
    string? Refusal)
{
    /// <summary>Whether this span carries numbers at all.</summary>
    public bool IsMeasured => Refusal is null;
}
