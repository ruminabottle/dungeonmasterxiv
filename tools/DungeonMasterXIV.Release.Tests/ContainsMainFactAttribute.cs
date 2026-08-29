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
            Skip = "This branch does not contain origin/main, so the working tree is NOT the merged "
                 + "tree and a pass here would describe a tree nobody is going to merge. "
                 + $"Rebase or merge main and the gate runs. ({detail})";
        }
    }

    private static readonly Lazy<(bool Contains, string Detail)> Containment = new(() =>
    {
        var (code, output, errors) = Git("merge-base --is-ancestor origin/main HEAD");

        // Exit 0 = ancestor, 1 = not. Anything else is git failing, and a git failure must not be
        // read as "contained" -- that would resurrect the green this whole attribute exists to stop.
        return code switch
        {
            0 => (true, "origin/main is an ancestor of HEAD"),
            1 => (false, "origin/main is not an ancestor of HEAD"),
            _ => (false, $"git could not answer (exit {code}): {errors.Trim()}{output.Trim()}"),
        };
    });

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
