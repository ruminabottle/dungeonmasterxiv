using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Whether the join flow's name box should be re-filled from settings (A-1.2n).
/// </summary>
/// <remarks>
/// <para>
/// <b>A settings default may PRE-FILL the join-flow field; it may not REPLACE it.</b> That is the
/// whole of A-1.2n's second sentence, and it makes the pre-fill a three-state decision rather than
/// an assignment: the source may have changed, the user may have typed, and those two combine
/// differently in each case.
/// </para>
/// <para>
/// <b>It lives here rather than in the window because a rule with no possible test is where a defect
/// sits unseen.</b> No test project links the plugin, so anything decided inside
/// <c>SessionWindow</c> can be read but never exercised. A source scan proves the control is
/// present; nothing it can do proves the rule is right. Different claims, and only this one has a
/// defect surface.
/// </para>
/// <para>
/// <b>It decides nothing about WHICH name is the source.</b> Whether that is the stored alias or the
/// character name is settled before this sees it. This compares three strings and says whether to
/// overwrite one of them.
/// </para>
/// </remarks>
public static class JoinFlowName
{
    /// <summary>
    /// Whether the field should be overwritten with <paramref name="fromSettings"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Compared against what was LAST SEEDED, not against the current setting</b>, and that is the
    /// distinction the rule turns on. It is what tells "the user has not touched this" apart from
    /// "the user deliberately typed the default back" — the second must survive, because someone who
    /// types a value means it.
    /// </para>
    /// <para>
    /// <b>Both conditions are load-bearing.</b> Dropping the first makes every frame overwrite the
    /// field, so nothing can be typed at all. Dropping the second overwrites a player's own edit the
    /// moment they switch character — which is the case a once-only seed gets wrong in the other
    /// direction, leaving them sending the previous character's name.
    /// </para>
    /// </remarks>
    /// <param name="fromSettings">The name the settings currently imply.</param>
    /// <param name="lastSeeded">What this rule last wrote into the field; empty if never.</param>
    /// <param name="typed">What the field holds now.</param>
    /// <returns>True when the field should be replaced and re-seeded.</returns>
    public static bool ShouldReplace(string fromSettings, string lastSeeded, string typed)
    {
        ArgumentNullException.ThrowIfNull(fromSettings);
        ArgumentNullException.ThrowIfNull(lastSeeded);
        ArgumentNullException.ThrowIfNull(typed);

        return !string.Equals(fromSettings, lastSeeded, StringComparison.Ordinal)
            && string.Equals(typed, lastSeeded, StringComparison.Ordinal);
    }
}
