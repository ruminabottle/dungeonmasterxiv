namespace DungeonMasterXIV.Net;

/// <summary>
/// What the roster region calls itself to a joined player (R-1.3f).
/// </summary>
/// <remarks>
/// <para>
/// <b>A heading is a CLAIM about what is below it, which is why this is a value and not a literal in
/// the window.</b> The roster a player receives structurally omits the DM — the host is not on its
/// own <c>Recipients</c>, so it is never in what it sends, and DMXENG-33 is the other half. A region
/// reading <i>"everyone in this session"</i> would therefore not merely omit the DM: it would TELL A
/// PLAYER THE DM IS NOT HERE. That is a false statement to a user rather than a missing feature —
/// the same defect as a control labelled with a promise it does not keep.
/// </para>
/// <para>
/// <b>It lives in Core because a decision that cannot be tested is a comment.</b> The test project
/// references Core alone and may never reference the plugin, so a rule kept in the window can only
/// be checked by reading source — and a source check on the CONSTANT was defeated in one line by
/// leaving the constant honest and passing a literal to the draw call instead, with every test
/// green. Same family as a check that reads only the first occurrence: it guarded the value while
/// nothing guarded that the value was USED. As a Core value there is nothing to bypass.
/// </para>
/// <para>
/// <b>Narrow on purpose, and it should be WIDENED by DMXENG-33 rather than defended forever.</b> Once
/// the roster carries the host, the broader claim becomes true and the tests pinning this wording
/// are supposed to fail.
/// </para>
/// </remarks>
public static class RosterHeading
{
    /// <summary>
    /// The heading shown above the roster. True of exactly what the roster shows today.
    /// </summary>
    public const string Text = "Players in this session:";
}
