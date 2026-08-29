using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Rolls;

/// <summary>
/// Decides whether an evaluation needs to say, in words, that nothing survived it (A-2.3b).
/// </summary>
/// <remarks>
/// <para>
/// <b>R-2.1 HAS TWO SENTENCES AND ONLY THE FIRST HAD A CRITERION.</b> <i>"Malformed notation is
/// refused with a message naming what was wrong"</i> was covered by A-2.3. <i>"It never silently
/// rolls something else"</i> had none — so a requirement with two clauses and one criterion looked
/// fully covered, because the criterion that existed passed. A-2.3b is the missing half, and this
/// type is where it is decided.
/// </para>
/// <para>
/// <b>THE TEST IS SURVIVAL, NOT THE TOTAL, AND THE CRITERION SAYS SO AFTER STRIKING ITS OWN NARROWER
/// EXAMPLE.</b> A clause reading <i>"a build that returns a total of zero without stating that
/// nothing survived fails"</i> was struck within the hour, because <c>4d6dl4+100</c> <b>drops every
/// die and totals 100</b> — by the rule it must say so, by the struck clause it did not fail. Keying
/// this on <c>Total == 0</c> would reproduce exactly the substitution the Spec Owner struck, and it
/// is the case a reader is least likely to notice, because the total looks ordinary.
/// </para>
/// <para>
/// <b>An expression with NO dice is not an expression whose dice all died.</b> <c>2+2</c> rolls
/// nothing, so there is nothing to report and no notice is produced. That distinction is what stops
/// the notice being unfalsifiable: a build that always announced it would pass a test which only
/// ever checks the announcing case.
/// </para>
/// </remarks>
internal static class RollSurvival
{
    /// <summary>The wording used when every die rolled was dropped or rerolled away.</summary>
    public const string NothingSurvived =
        "Every die was dropped or rerolled away, so no die counted towards the total.";

    /// <summary>
    /// The words to attach to an outcome, or null when there is nothing to say.
    /// </summary>
    /// <param name="dice">Every die rolled, kept and not-kept alike.</param>
    public static string? NoticeFor(IReadOnlyList<RolledDie> dice) =>
        dice.Count > 0 && !dice.Any(die => die.Kept) ? NothingSurvived : null;
}
