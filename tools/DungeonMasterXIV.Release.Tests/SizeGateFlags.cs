using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// Comparing two flag measurements, and saying what newly crossed (DMXENG-107).
/// </summary>
/// <remarks>
/// <para>
/// <b>SEPARATE FROM <see cref="SizeGate"/> ON THE MERITS, NOT TO MAKE A NUMBER SMALLER.</b> The
/// standard's test for a size flag is <i>the number of reasons the file could change</i>, and
/// <c>SizeGate</c> was acquiring a fourth: measurement, refusal, delta, and report wording. Those
/// last two change for reasons the first two do not — a change to how a crossing READS is not a
/// change to what a crossing IS.
/// </para>
/// <para>
/// <b>Disclosed rather than quietly done, because this PR is the one that teaches the gate to report
/// flag crossings.</b> Left whole, <c>SizeGate.cs</c> went 194 → 318 lines and newly crossed the file
/// flag of 300. Reporting that and splitting on the merits is the behaviour the ticket is asking the
/// gate to make possible; crossing it silently in this PR of all PRs would not be.
/// </para>
/// <para>
/// <b>The MEASUREMENT stays in <see cref="SizeGate"/> and is shared with the block path.</b> Two
/// readers of the same source that could disagree are exactly the drift the gate already polices
/// against the tool.
/// </para>
/// <para>
/// <b>KNOWN FALSE POSITIVE, FOUND BY RUNNING THIS AGAINST ITS OWN PR: A RENAME READS AS A NEW
/// CROSSING.</b> Identity is <see cref="Breach.Key"/> — file, row and unit — so renaming a member
/// that was ALREADY over a flag retires one key and introduces another, and the new one has no
/// history to be compared against. Verbatim output of this mechanism run on the change that
/// introduced it:
/// <code>
/// before crossings: 3   after: 3   NEWLY: 1
///   SizeGate.cs  method MeasureAgainst(3) = 55, NEWLY OVER THE METHOD FLAG of 40
/// </code>
/// <c>BreachesIn(2)</c> was 54 lines and already over; it became <c>MeasureAgainst(3)</c> at 55.
/// <b>Nothing got worse and the report said something.</b> The count of crossings did not move —
/// 3 before, 3 after — which is the signature to look for.
/// <b>Left as a named limitation rather than patched:</b> distinguishing a rename from a genuinely
/// new crossing needs similarity matching, and a heuristic that silently pairs two units is a way
/// for a REAL crossing to be explained away. Noise a reader can recognise beats silence they cannot.
/// </para>
/// </remarks>
internal static class SizeGateFlags
{
    /// <summary>
    /// Which flags <paramref name="after"/> crosses that <paramref name="before"/> did not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NEWLY crossed, by <see cref="Breach.Key"/> — file, row and unit.</b> That is the case
    /// DMXENG-66 exposed: <c>InboundFrame</c> went 236 UNDER to 261 OVER across one PR and every gate
    /// report was silent, because the gate only ever looked at blocks.
    /// </para>
    /// <para>
    /// <b>A crossing that was ALREADY over and got worse is deliberately NOT reported, and that is a
    /// decision rather than an oversight.</b> The ticket rules a newly-crossed report; whether
    /// "already over, now further over" deserves the same conversation is a separate question, and
    /// building an answer to it here would settle it without anyone ruling it. Named so the next
    /// reader meets the choice instead of inferring it from silence.
    /// </para>
    /// </remarks>
    /// <param name="before">Flag crossings in the tree as it was.</param>
    /// <param name="after">Flag crossings in the tree as it is.</param>
    internal static IReadOnlyList<Breach> NewlyCrossedFlags(
        IReadOnlyList<Breach> before, IReadOnlyList<Breach> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var already = before.Select(crossing => crossing.Key).ToHashSet(StringComparer.Ordinal);
        return after.Where(crossing => !already.Contains(crossing.Key)).ToList();
    }

    /// <summary>
    /// The report the Code Reviewer reads. <b>Empty when nothing newly crossed</b> — silence is the
    /// ordinary outcome, and a report that says something every time is one nobody reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>BUG-111: the standing and the margin BOTH name their own row.</b> A bare "margin 11" says
    /// nothing about which limit it is 11 away from, and an absent row is not read as absent — the
    /// reader fills the gap with whichever row they arrived asking about.
    /// </para>
    /// <para>
    /// <b>IT STATES THE TOTALS IT WAS COMPUTED FROM, AND THAT IS THE FIX FOR THIS TYPE'S OWN
    /// DOCUMENTED FALSE POSITIVE (#216 review).</b> The rename caveat above is the disambiguator, and
    /// the first draft left it in <c>&lt;remarks&gt;</c> — <b>where no reader of the REPORT will ever
    /// look.</b> A crossing count that did not move while a new crossing appeared is the rename
    /// signature, and it is only visible if the totals travel WITH the rows.
    /// </para>
    /// <para>
    /// <b>It takes the two measurements rather than a precomputed list, so the totals CANNOT disagree
    /// with the rows.</b> Handing it both a crossing list and its totals would let a caller supply
    /// sets that do not correspond — a header stating a total the rows were not drawn from is a
    /// worse defect than the omission it replaced.
    /// </para>
    /// </remarks>
    /// <param name="before">Flag crossings in the tree as it was.</param>
    /// <param name="after">Flag crossings in the tree as it is.</param>
    internal static IReadOnlyList<string> FlagReport(
        IReadOnlyList<Breach> before, IReadOnlyList<Breach> after)
    {
        var crossings = NewlyCrossedFlags(before, after);
        if (crossings.Count == 0)
        {
            return [];
        }

        var totals = $"{before.Count} flag crossing(s) before, {after.Count} after, "
            + $"{crossings.Count} NEWLY crossed.";
        var caution = before.Count == after.Count
            ? " The crossing count did NOT move while a new one appeared — that is the rename"
                + " signature (one key retires, another arrives). Check whether the unit was renamed"
                + " before reading this as new length."
            : string.Empty;

        return [totals + caution, .. crossings.Select(Describe)];
    }

    private static string Describe(Breach crossing) =>
        $"{crossing.File}  {crossing.Row} {crossing.Unit} = {crossing.Value}, "
        + $"NEWLY OVER THE {crossing.Row.ToUpperInvariant()} FLAG of {crossing.Capacity} "
        + $"({-crossing.Margin} over the {crossing.Row} flag). Not a refusal: flags are a conversation.";
}
