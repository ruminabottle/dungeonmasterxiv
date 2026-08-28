namespace DungeonMasterXIV.Net;

/// <summary>
/// What the join field accepts (A-1.18).
/// </summary>
/// <remarks>
/// <para>
/// <b>In Core so a test can CALL it, which is the whole reason it exists (DMXENG-15).</b> The
/// decision was one expression inside <c>DrawJoining</c>, and no test project references the
/// plugin, so the only witness available was
/// <c>CopiedCodePastesIntoTheJoinFieldTests.WhatTheJoinFieldAccepts</c> — a private
/// RE-IMPLEMENTATION of <see cref="SessionCode.TryParse"/> whose link to the join field was the
/// doc comment <i>"mirrors DrawJoining"</i>. A comment is not an assertion: the mirror would have
/// stayed green while the field it mirrors changed underneath it.
/// </para>
/// <para>
/// <b>Why this is not merely <see cref="SessionCode.TryParse"/> under another name.</b> That
/// method is the general parser and has other callers; this is the answer to one narrower
/// question — what the JOIN FIELD accepts from a paste. They agree today, and A-1.18 is a
/// statement about this one, so it needs somewhere to be true. Anyone narrowing what the field
/// takes changes this and the tests follow; anyone changing the shared parser is told by them.
/// </para>
/// <para>
/// <b>Pairs with <see cref="JoinFlowName"/>, deliberately.</b> The join flow asks the user for two
/// things, a code and a name, and both decisions now live beside each other in Core while the
/// drawing of them lives in <c>JoinFlowView</c>.
/// </para>
/// </remarks>
public static class JoinFlowCode
{
    /// <summary>Whether <paramref name="typed"/> is a code the join field accepts.</summary>
    /// <param name="typed">Exactly what is in the input box — pasted or typed, unaltered.</param>
    /// <param name="code">The parsed code, when accepted.</param>
    /// <returns>Whether a join may be requested with it.</returns>
    /// <remarks>
    /// Passed the box's contents UNALTERED. A-1.18 requires a copied code to be accepted verbatim,
    /// and the grouped display form works because <see cref="SessionCode.TryParse"/> strips
    /// hyphens — trimming or reshaping here would move that guarantee out from under the test that
    /// holds it.
    /// </remarks>
    public static bool Accepts(string typed, out SessionCode code) =>
        SessionCode.TryParse(typed, out code);
}
