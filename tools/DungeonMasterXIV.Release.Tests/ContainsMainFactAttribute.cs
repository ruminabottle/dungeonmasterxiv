using System;
using System.Diagnostics;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// A fact that runs only where the working tree IS the merged tree, and skips saying why when it is not.
/// </summary>
/// <remarks>
/// <para>
/// <b>THREE OUTCOMES, NOT TWO (DMXENG-70).</b> The size gate measures the working tree, and that is
/// only the merged tree if this branch already contains <c>origin/main</c>. On a branch that does
/// not, a green would mean <i>"some tree was clean"</i> rather than <i>"the merged tree is clean"</i>
/// — which is the exact failure class the gate exists to end, occurring inside the gate. So the
/// honest third outcome is a SKIP that names what is missing.
/// </para>
/// <para>
/// <b>A SKIP RATHER THAN A FAILURE, AND THAT IS RULED RATHER THAN SOFT.</b> Refusing outright would
/// turn the suite red for every engineer mid-development, and a habitually red suite trains people to
/// read past it — worse than the hole. The skip becomes a refusal at the merge gate instead: the
/// Deployment Manager does not merge a PR whose suite skipped this test, so the honest outcome
/// propagates rather than being swallowed.
/// </para>
/// <para>
/// <b>Not a silent early return.</b> A test that quietly does nothing where it cannot do the real
/// check still reports as a pass and is counted as coverage.
/// </para>
/// </remarks>
public sealed class ContainsMainFactAttribute : FactAttribute
{
    /// <summary>Skips, with the reason, when this branch does not contain <c>origin/main</c>.</summary>
    public ContainsMainFactAttribute()
    {
        var (contains, detail) = Containment.Value;
        if (!contains)
        {
            // CAUSE-NEUTRAL WRAPPER, AND THE DETAIL CARRIES THE CAUSE (BUG-124). This used to open
            // "This branch does not contain origin/main" and then append the real detail in
            // brackets. On any arm except the ancestry one that is a FALSE ASSERTION FOLLOWED BY
            // THE TRUE ONE CONTRADICTING IT -- git failing to answer does not mean the branch is
            // behind, and neither does an unreachable origin. It was already wrong on the
            // git-could-not-answer arm; adding two more arms would have made it wrong three times.
            //
            // So the consequence is stated (which is true on every arm) and the cause is left to
            // the detail, each of which now ends in the action a reader should take.
            Skip = "SIZE GATE NOT RUN, so this tree is not known to be the merged tree and a pass "
                 + $"here would describe a tree nobody is going to merge: {detail}";
        }
    }

    private static readonly Lazy<(bool Contains, string Detail)> Containment = new(() =>
    {
        // CURRENCY BEFORE ANCESTRY, AND THE ORDER IS THE FIX (BUG-124). `merge-base` reads
        // refs/remotes/origin/main, which is a LOCAL CACHE as fresh as this clone's last fetch --
        // not a fact about the remote. A stale cache is an ancestor of a tree that is itself behind
        // real main, so the ancestor question answers YES and the gate RUNS AND PASSES against a
        // tree nobody will merge. Measured: HEAD one commit behind main with a six-commit-old cached
        // ref reports contained, and the gate returns 15 passed, 0 skipped.
        //
        // That is the one sentence the three outcomes exist to make impossible -- "could not
        // validate" reported as "clean" -- arriving through the check meant to prevent it.
        var (remoteCode, remote, remoteErrors) = Git("ls-remote origin refs/heads/main");
        var remoteHead = remote.Split('\t')[0].Trim();

        // ASKING COSTS A NETWORK CALL AND NOT ASKING COSTS THE GUARANTEE. `ls-remote` reads the
        // remote without fetching, so it changes no ref in this clone -- but it can fail, and a
        // failure here must SKIP rather than fall through. Degrading to the local answer when the
        // remote is unreachable is exactly today's behaviour, which is the bug.
        if (remoteCode != 0 || remoteHead.Length == 0)
        {
            return (false, "could not reach origin to establish that the cached origin/main is "
                + "current, so containment cannot be decided and a pass here would describe a tree "
                + $"this check has not validated. ({remoteErrors.Trim()})");
        }

        var (cachedCode, cached, _) = Git("rev-parse refs/remotes/origin/main");
        var cachedHead = cached.Trim();

        if (cachedCode != 0 || cachedHead.Length == 0)
        {
            return (false, "this clone has no refs/remotes/origin/main to compare against origin");
        }

        if (!string.Equals(cachedHead, remoteHead, StringComparison.Ordinal))
        {
            return (false, $"the cached origin/main ({Short(cachedHead)}) is STALE -- origin is at "
                + $"{Short(remoteHead)}. Containment measured against a stale cache is the defect "
                + "this reports rather than a result. Fetch and re-run.");
        }

        var (code, output, errors) = Git("merge-base --is-ancestor origin/main HEAD");

        // Exit 0 = ancestor, 1 = not. Anything else is git failing, and a git failure must not be
        // read as "contained" -- that would resurrect the green this whole attribute exists to stop.
        return code switch
        {
            0 => (true, $"origin/main ({Short(remoteHead)}) is current and an ancestor of HEAD"),
            1 => (false, "origin/main is not an ancestor of HEAD -- merge or rebase main and the gate runs"),
            _ => (false, $"git could not answer (exit {code}): {errors.Trim()}{output.Trim()}"),
        };
    });

    private static string Short(string sha) => sha.Length >= 7 ? sha[..7] : sha;

    private static (int Code, string Output, string Errors) Git(string arguments)
    {
        using var git = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = TheBuild.RepositoryRoot().FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("git did not start");

        var output = git.StandardOutput.ReadToEnd();
        var errors = git.StandardError.ReadToEnd();
        git.WaitForExit();
        return (git.ExitCode, output, errors);
    }
}
