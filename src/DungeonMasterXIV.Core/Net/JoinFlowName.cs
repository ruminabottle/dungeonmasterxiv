using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The join flow's name box after settings have had their say (A-1.2n).
/// </summary>
/// <remarks>
/// <para>
/// <b>A settings default may PRE-FILL the join-flow field; it may not REPLACE it.</b> That is
/// A-1.2n's second sentence, and it makes the pre-fill a decision rather than an assignment.
/// </para>
/// <para>
/// <b>It lives here rather than in the window because a rule with no possible test is where a defect
/// sits unseen.</b> No test project links the plugin, so anything decided inside
/// <c>SessionWindow</c> can be read but never exercised.
/// </para>
/// <para>
/// <b>And it returns BOTH values together, which is the other half of the same argument.</b>
/// Extracting a decision does not extract its invariant: the field and the seed it was written from
/// must move as one, and while the window updated them separately that pairing sat exactly where the
/// rule had just been taken from — untestable, and now looking covered, which is worse. A single
/// return makes updating one without the other unrepresentable rather than merely discouraged.
/// </para>
/// <para>
/// <b>It decides nothing about WHICH name is the source.</b> Whether that is the stored alias or the
/// character name is settled before this sees it.
/// </para>
/// </remarks>
public static class JoinFlowName
{
    /// <summary>
    /// What the name box and its seed should hold, given what settings now imply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Returns the pair, never one half.</b> <see cref="PreFilledName.SeededFrom"/> is the record
    /// of what this rule last wrote; feed it a value the rule did not write and every later decision
    /// is made on a false premise, with nothing able to notice.
    /// </para>
    /// <para>
    /// <b>What the comparison against <paramref name="lastSeeded"/> actually buys, stated precisely
    /// because an earlier version of this comment overclaimed it.</b> It does NOT distinguish a user
    /// who never touched the field from one who deliberately typed the seeded value back — those two
    /// are identical inputs and no state here can separate them, because the window has no edit
    /// signal to offer. What it guarantees is narrower and sufficient: <b>a field holding anything
    /// this rule did not write is never overwritten.</b> Comparing against
    /// <paramref name="fromSettings"/> instead would break pre-fill outright, since the field would
    /// match the source the instant it was filled.
    /// </para>
    /// <para>
    /// <b>Both conditions are load-bearing.</b> Without the first, every frame overwrites the field
    /// and nothing can be typed. Without the second, a player's own name is replaced the moment they
    /// switch character.
    /// </para>
    /// </remarks>
    /// <param name="fromSettings">The name the settings currently imply.</param>
    /// <param name="lastSeeded">What this rule last wrote into the field; empty if never.</param>
    /// <param name="typed">What the field holds now.</param>
    /// <returns>The field and its seed, together.</returns>
    public static PreFilledName Resolve(string fromSettings, string lastSeeded, string typed)
    {
        ArgumentNullException.ThrowIfNull(fromSettings);
        ArgumentNullException.ThrowIfNull(lastSeeded);
        ArgumentNullException.ThrowIfNull(typed);

        var sourceMoved = !string.Equals(fromSettings, lastSeeded, StringComparison.Ordinal);
        var fieldIsStillOurs = string.Equals(typed, lastSeeded, StringComparison.Ordinal);

        return sourceMoved && fieldIsStillOurs
            ? new PreFilledName(fromSettings, fromSettings)
            : new PreFilledName(typed, lastSeeded);
    }
}

/// <summary>
/// The join flow's name box and the seed it was written from, which move together or not at all.
/// </summary>
/// <param name="Typed">What the field should hold.</param>
/// <param name="SeededFrom">What the pre-fill rule last wrote into it; empty if never.</param>
public readonly record struct PreFilledName(string Typed, string SeededFrom);
