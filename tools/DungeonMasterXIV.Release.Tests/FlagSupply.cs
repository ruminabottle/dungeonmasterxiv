using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The half DMXENG-107 left out: something that reads a tree TWICE and hands the pair to the
/// reporter (DMXENG-112).
/// </summary>
/// <remarks>
/// <para>
/// <b>#216 BUILT A REPORTER WITH NO SUPPLY.</b> Every caller of <c>NewlyCrossedFlags</c> and
/// <c>FlagReport</c> was a test handing over a constructed pair, while the real tree walk called
/// <c>BreachesIn</c> and never touched the flag path. So on a real tree the gate measured blocks
/// only — exactly as before #216 — and <c>ConfigWindow.cs</c> went 264 → 324 with the suite printing
/// nothing. <b>Every test proved the function works when GIVEN a pair; none proved anyone gives it
/// one.</b>
/// </para>
/// <para>
/// <b>WHERE "BEFORE" COMES FROM: <c>origin/main</c>, READ THROUGH GIT.</b> Named with what it cannot
/// catch, because each option fails differently:
/// <list type="bullet">
/// <item><b>Chosen — the base ref.</b> It is the tree the PR is actually measured against, it needs
/// no file anyone can forget to update, and it is already how this gate decides whether it may run
/// at all.</item>
/// <item><b>Rejected — a baseline FILE.</b> Ruled out as DMXENG-107's shape 3, and the reasoning is
/// unchanged: an empty floor fails OPEN and makes the check vacuous, which
/// <c>SizeGateBaseline</c>'s own doc already had to write a guard against.</item>
/// <item><b>Rejected — a caller-supplied pair.</b> That is what exists, and it is the defect.</item>
/// </list>
/// </para>
/// <para>
/// <b>AND IT DOES NOT SEE UNTRACKED FILES — MEASURED, AND THE FIRST ATTEMPT TO FIX IT WAS INERT.</b>
/// <c>git diff --name-only</c> returned EMPTY with two new files on disk, so I unioned in
/// <c>ls-files --others</c>. <b>That bought nothing and I only caught it by comparing two runs:</b>
/// "0 of 2 changed file(s) are in intake" became "2 of 2" once the files were COMMITTED, because
/// <see cref="SizeGateIntake.Files"/> is <c>git ls-files</c> — <b>tracked only</b> — so an untracked
/// path is filtered out one line later regardless. The union was a tested mechanism with no effect:
/// <b>the very shape this ticket exists to fix, reproduced inside its own fix.</b> Removed rather
/// than kept as speculative generality. <b>The consequence stands and is the intake's to change, not
/// this type's: a new file is measured once it is committed, and the block gate has always behaved
/// the same way.</b>
/// </para>
/// <para>
/// <b>WHAT THE CHOSEN OPTION CANNOT CATCH, stated rather than discovered later.</b> A delta sees two
/// endpoints: a flag crossed and then uncrossed inside the branch is invisible, and so is one that
/// was ALREADY over on the base ref and got worse — the second is
/// <c>NewlyCrossedFlags</c>' own documented and deliberate exclusion. It also cannot run at all where
/// the base ref is unavailable, which is why the live consumer carries
/// <see cref="ContainsMainFactAttribute"/> rather than degrading to a one-tree answer: a report
/// computed against a tree we could not read is the silent-pass this gate exists to end.
/// </para>
/// </remarks>
internal static class FlagSupply
{
    /// <summary>
    /// Walks <paramref name="files"/>, measures each on both sides, and returns the report.
    /// </summary>
    /// <remarks>
    /// <b>The readers are parameters so the walk itself can be driven.</b> The thing that was missing
    /// is not the reporting logic — it is that nobody joined a tree to it — so the join is what has
    /// to be testable. A test that hands <c>FlagReport</c> two lists proves what is already proven.
    /// </remarks>
    /// <param name="files">Repository-relative paths to measure.</param>
    /// <param name="before">
    /// The file's source at the base ref, or <c>null</c> where it did not exist there. <b>Null is not
    /// an error:</b> a file added by this branch has no prior crossings, so one that arrives over a
    /// flag is correctly newly crossed.
    /// </param>
    /// <param name="after">The file's source as it is now.</param>
    internal static IReadOnlyList<string> ReportFor(
        IReadOnlyList<string> files, Func<string, string?> before, Func<string, string> after)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var was = new List<Breach>();
        var now = new List<Breach>();

        foreach (var path in files)
        {
            if (before(path) is { } prior)
            {
                was.AddRange(SizeGate.FlagCrossingsIn(path, prior).Breaches);
            }

            now.AddRange(SizeGate.FlagCrossingsIn(path, after(path)).Breaches);
        }

        return SizeGateFlags.FlagReport(was, now);
    }

    /// <summary>A file's source at <paramref name="reference"/>, or null if it is not there.</summary>
    /// <param name="reference">The git ref to read from.</param>
    /// <param name="path">Repository-relative path.</param>
    internal static string? SourceAt(string reference, string path)
    {
        var (code, output, _, timedOut) = ContainsMainFactAttribute.Git($"show {reference}:{path}");
        return code == 0 && !timedOut ? output : null;
    }

    /// <summary>
    /// The files this tree changed against <paramref name="reference"/>, or null if git could not say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Restricting the walk to CHANGED files is an equivalence, not a shortcut.</b> A file
    /// identical on both sides measures identically on both sides, so it cannot newly cross. The walk
    /// is over ~150 files and each read is a process; this is what keeps the gate cheap enough to
    /// stay in the ordinary suite.
    /// </para>
    /// <para>
    /// <b>IT RETURNS NULL RATHER THAN AN EMPTY LIST WHEN GIT FAILS, AND THE DISTINCTION IS THE WHOLE
    /// RISK.</b> An empty list means "nothing changed" and yields a silent, clean report — which is
    /// indistinguishable from the answer this gate exists to stop being faked. Null means "could not
    /// ask", and the caller must refuse rather than report silence.
    /// </para>
    /// </remarks>
    /// <param name="reference">The git ref to compare against.</param>
    internal static IReadOnlyList<string>? ChangedAgainst(string reference)
    {
        var (code, output, _, timedOut) = ContainsMainFactAttribute.Git($"diff --name-only {reference}");
        return Parse(code, output, timedOut);
    }

    /// <summary>
    /// git's answer as a path list, or null when it did not answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NULL RATHER THAN AN EMPTY LIST WHEN GIT FAILS, AND THE DISTINCTION IS THE WHOLE RISK.</b>
    /// Empty means "nothing changed" and yields a silent, clean report — indistinguishable from the
    /// answer this gate exists to stop being faked. Null means "could not ask", and the caller must
    /// refuse rather than report silence.
    /// </para>
    /// <para>
    /// Pure, and separate from the git call, for the reason <c>ContainsMainFactAttribute.Decide</c>
    /// takes its runner as a parameter: an arm that cannot be driven is an arm nothing checks.
    /// </para>
    /// </remarks>
    /// <param name="code">git's exit code.</param>
    /// <param name="output">Its standard output.</param>
    /// <param name="timedOut">Whether it was killed for exceeding its bound.</param>
    internal static IReadOnlyList<string>? Parse(int code, string output, bool timedOut) =>
        code != 0 || timedOut
            ? null
            : output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
