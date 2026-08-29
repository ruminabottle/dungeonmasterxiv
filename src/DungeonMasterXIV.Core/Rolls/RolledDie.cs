namespace DungeonMasterXIV.Rolls;

/// <summary>
/// One die that was rolled: how many faces it had, what it showed, and whether it counted.
/// </summary>
/// <param name="Sides">The die's face count — the <c>6</c> in <c>4d6</c>.</param>
/// <param name="Value">The face it showed, always between 1 and <paramref name="Sides"/>.</param>
/// <param name="Kept">
/// Whether this die contributed to the total. <b>False dice are kept in the list rather than
/// removed</b> — see the remarks.
/// </param>
/// <remarks>
/// <para>
/// <b>EXPOSING THE INDIVIDUAL DICE IS A REQUIREMENT, NOT A DISPLAY NICETY (A-2.1a).</b> The
/// criterion states the reason and it is worth keeping next to the type: <i>a log showing totals
/// alone is unfalsifiable by construction — no test and no human could catch a bad roller.</i> A
/// total is one number that any wrong implementation can also produce. The dice behind it are what
/// make the total checkable, which is why this type exists at all.
/// </para>
/// <para>
/// <b>Dropped dice are recorded, not discarded.</b> <c>4d6kh3</c> keeps three and drops one, and the
/// dropped one is exactly what a reader needs to see to know the keep happened and happened
/// correctly. Removing it would leave three dice and a total that agree with each other and with a
/// roller that never rolled the fourth — the unfalsifiable shape again, one level down.
/// </para>
/// <para>
/// <b>Rerolled dice are recorded too</b>, as <see cref="Kept"/> false, so <c>2d20r1</c> shows the
/// discarded 1 beside its replacement rather than silently becoming a different roll.
/// </para>
/// </remarks>
public readonly record struct RolledDie(int Sides, int Value, bool Kept = true);
