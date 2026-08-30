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
        return Combine(Lines($"diff --name-only {reference}"), Lines("ls-files --others --exclude-standard"));
    }

    /// <summary>
    /// Tracked changes and untracked additions as one list, or null if either could not be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UNTRACKED FILES ARE INCLUDED BECAUSE <c>git diff</c> CANNOT SEE THEM, AND I MEASURED THAT
    /// RATHER THAN ASSUMING IT.</b> With two new files on disk,
    /// <c>git diff --name-only origin/main</c> returned EMPTY — so a brand-new file arriving over a
    /// flag would have been reported by nothing until somebody committed it. A gate that is silent
    /// on exactly the change an engineer is looking at when they run it is the same defect this
    /// ticket exists to fix, one step smaller.
    /// </para>
    /// <para>
    /// <b>Null propagates.</b> If either list is unreadable the answer is "could not ask", never a
    /// shorter list — a partial walk reported as a clean one is the fail-open arm.
    /// </para>
    /// <para>
    /// Pure, and separate from the git calls, for the reason <c>ContainsMainFactAttribute.Decide</c>
    /// takes its runner as a parameter: an arm that cannot be driven is an arm nothing checks.
    /// </para>
    /// </remarks>
    /// <param name="tracked">Paths git reported as changed, or null if it could not say.</param>
    /// <param name="untracked">Paths git reported as untracked, or null if it could not say.</param>
    internal static IReadOnlyList<string>? Combine(
        IReadOnlyList<string>? tracked, IReadOnlyList<string>? untracked) =>
        tracked is null || untracked is null
            ? null
            : [.. tracked.Concat(untracked).Distinct(StringComparer.Ordinal)];

    private static IReadOnlyList<string>? Lines(string arguments)
    {
        var (code, output, _, timedOut) = ContainsMainFactAttribute.Git(arguments);
        return code != 0 || timedOut
            ? null
            : output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
