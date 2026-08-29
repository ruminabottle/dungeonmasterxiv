using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    /// <param name="test">
    /// Supplied by the compiler as the name of the annotated method — never passed by a caller. It is
    /// here so the display name can keep the test's identity while carrying the reason.
    /// </param>
    public ContainsMainFactAttribute([CallerMemberName] string? test = null)
    {
        var (contains, detail) = Containment.Value;
        if (contains)
        {
            return;
        }

        // BUG-125: THE LEADING SENTENCE NAMED A CAUSE THE CHECK HAD NOT ESTABLISHED. `Containment`
        // returns contains:false on five arms and only ONE of them is "this branch is behind main":
        // git can fail, origin can be unreachable, the clone can have no cached ref, and the cached
        // ref can be stale -- and on a stale ref the branch may well contain main. The true reason
        // was appended in brackets AFTER the false one, so the message asserted something untrue and
        // then quietly contradicted itself. "Rebase or merge main" was wrong on the same four arms:
        // an instruction that will not help is worse than no instruction.
        //
        // So the wrapper states the CONSEQUENCE, which is true on every arm, and leaves the CAUSE
        // entirely to `detail` -- each of which now ends in the action for its own arm.
        Skip = "SIZE GATE NOT RUN, so this tree is not known to be the merged tree and a pass "
             + $"here would describe a tree nobody is going to merge: {detail}";

        // BUG-123: THE SKIP FIRED CORRECTLY AND NOBODY COULD SEE WHY. `Skip` is printed only at -v n
        // or above; the DEFAULT invocation prints the test's DISPLAY NAME and nothing else about a
        // skip. So on the invocation everyone actually runs, a reader got a name and had to already
        // know that test's skip condition to interpret it -- and "Skipped 1" beside "Failed 0" reads
        // as a pass to anyone not already counting.
        //
        // That matters more here than for an ordinary skip: this gate exists so that a green meaning
        // "some tree was clean" cannot be mistaken for one meaning "the MERGED tree is clean". The
        // skip is the only thing keeping those apart, so its reason being invisible reproduces the
        // very confusion the gate was built to end, one level up.
        //
        // The name is supplied by the compiler rather than written here, so a rename cannot leave a
        // stale label behind. And DisplayName is DISPLAY ONLY: --filter matches the fully qualified
        // method name and still finds this test while it is skipping (measured).
        DisplayName = SkippedDisplayName(test, detail);
    }

    /// <summary>
    /// What the default invocation prints instead of the bare method name when the gate cannot run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separated so the property can be asserted without a behind-main working tree: the runner's
    /// behaviour is measured, this is the part a test can hold.
    /// </para>
    /// <para>
    /// <b>IT REPEATS THE DETAIL RATHER THAN NAMING A CAUSE.</b> Not contained is not the only way
    /// <see cref="Containment"/> answers false — a git failure answers false too, deliberately, and
    /// says so in its detail. A display name that asserted non-containment would state something
    /// FALSE on that arm, having been written for the arm its author had in mind. Repeating the
    /// detail is also why a reason added later is visible here without this line being revisited.
    /// </para>
    /// </remarks>
    /// <param name="test">The annotated method's name.</param>
    /// <param name="detail">Why the gate could not run, from the containment check itself.</param>
    internal static string SkippedDisplayName(string? test, string detail) =>
        $"{test} -- SIZE GATE NOT RUN, so this tree is not known to be the merged tree: {detail}";

    private static readonly Lazy<(bool Contains, string Detail)> Containment = new(() => Decide(Git));

    /// <summary>Decides containment from whatever the supplied git returns.</summary>
    /// <remarks>
    /// <para>
    /// <b>THE RUNNER IS A PARAMETER SO THE ARMS CAN BE DRIVEN.</b> Every branch below depends on a
    /// network condition that cannot be created from a test — and the two that BUG-126 added were
    /// measurably unguarded while this was a closure: deleting the timeout arm, and dropping the
    /// bound from the call, both left the whole suite green. A seam is what makes the difference
    /// between a reason that exists and a reason that is reached.
    /// </para>
    /// <para>
    /// It takes the runner rather than the results so that the ARGUMENTS are observable too. The
    /// bound being passed is half of this fix, and a test that only saw return values could not
    /// tell a bounded call from an unbounded one.
    /// </para>
    /// </remarks>
    /// <param name="git">Runs a git command with an optional bound, as <see cref="Git"/> does.</param>
    internal static (bool Contains, string Detail) Decide(
        Func<string, TimeSpan?, (int Code, string Output, string Errors, bool TimedOut)> git)
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
        var (remoteCode, remote, remoteErrors, remoteTimedOut) =
            git("ls-remote origin refs/heads/main", RemoteTimeout);

        // UNREACHABLE AND UNRESPONSIVE ARE DIFFERENT FACTS AND GET DIFFERENT REASONS (BUG-126). A
        // refused connection returns at once because the host sends RST; a DROPPED one sends
        // nothing, so the wait below is the only thing that ends it. Checked BEFORE the exit-code
        // arm so the timeout cannot be reported as "could not reach origin", which is the other
        // arm's fact and would name a cause this check did not observe.
        if (remoteTimedOut)
        {
            return (false, TimedOutDetail);
        }

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

        var (cachedCode, cached, _, _) = git("rev-parse refs/remotes/origin/main", null);
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

        var (code, output, errors, _) = git("merge-base --is-ancestor origin/main HEAD", null);

        // Exit 0 = ancestor, 1 = not. Anything else is git failing, and a git failure must not be
        // read as "contained" -- that would resurrect the green this whole attribute exists to stop.
        return code switch
        {
            0 => (true, $"origin/main ({Short(remoteHead)}) is current and an ancestor of HEAD"),
            1 => (false, "origin/main is not an ancestor of HEAD -- merge or rebase main and the gate runs"),
            _ => (false, $"git could not answer (exit {code}): {errors.Trim()}{output.Trim()}"),
        };
    }

    /// <summary>How long origin gets to answer the currency check before the gate stops waiting.</summary>
    /// <remarks>
    /// <para>
    /// <b>TEN SECONDS, AND THE NUMBER IS THE CODEBASE'S OWN.</b> <c>JoinAttempt.ContactTimeout</c>
    /// and <c>HostSession.RegistrationTimeout</c> are both ten seconds, and both answer the same
    /// question this one does — how long a remote party gets to respond before we stop waiting.
    /// </para>
    /// <para>
    /// <b>Generous on purpose, because the two errors are not symmetric.</b> A reachable origin
    /// answers in about a second (measured twice, independently). Timing out too eagerly produces a
    /// SKIP, and a skip blocks the merge gate — so a false one costs an engineer a merge, on a
    /// network that was merely slow. Waiting too long costs ten seconds, and only ever on a run
    /// where the network is already broken. The healthy path never pays it.
    /// </para>
    /// </remarks>
    internal static readonly TimeSpan RemoteTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The exit code reported when git was killed for exceeding its bound.</summary>
    /// <remarks>
    /// Distinct from any code git returns itself, so a timeout cannot be mistaken for git having
    /// answered. Callers should branch on <c>TimedOut</c> rather than on this value.
    /// </remarks>
    internal const int TimedOutCode = -1;

    /// <summary>Why the gate could not run when origin was reached but never replied.</summary>
    /// <remarks>
    /// <para>
    /// <b>IT MUST NOT SAY ORIGIN COULD NOT BE REACHED, because origin WAS reached.</b> The
    /// connection was accepted and then nothing came back, which is a different fact with a
    /// different cause — a VPN, proxy or DNS sink rather than being offline — and pointing a reader
    /// at the wrong one costs them the time it takes to disprove it. Naming a cause the check did
    /// not observe is BUG-125's whole subject.
    /// </para>
    /// <para>
    /// Extracted for the same reason <see cref="SkippedDisplayName"/> is: a test can hold this
    /// without needing an unresponsive network to exist.
    /// </para>
    /// </remarks>
    internal static string TimedOutDetail =>
        $"origin was reached but did not answer within {RemoteTimeout.TotalSeconds:F0}s, so whether "
        + "the cached origin/main is current could not be established. That is a responsive-network "
        + "problem rather than an offline one -- a VPN, proxy or DNS sink will do it, and being "
        + "genuinely offline will not. Check the path to origin and re-run.";

    private static string Short(string sha) => sha.Length >= 7 ? sha[..7] : sha;

    /// <summary>
    /// Runs git, optionally refusing to wait longer than <paramref name="bound"/> for it to answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE READS ARE ASYNCHRONOUS BECAUSE A SYNCHRONOUS ONE BLOCKS BEFORE ANY TIMEOUT IS
    /// CONSULTED.</b> <c>ReadToEnd()</c> returns only at end of stream, and a hung child holds its
    /// pipe open — so the read, which runs first, is where an unresponsive origin actually stops.
    /// Measured: against a socket that accepts and never answers, <c>ReadToEnd()</c> was still
    /// blocked after 6s while <c>WaitForExit(1s)</c> returned false. <b>Bounding only
    /// <c>WaitForExit</c> would read as a fix and still hang</b>, because control never reaches it.
    /// </para>
    /// <para>
    /// Killing the tree is what releases the reads: closing the pipes is what ends them, and git
    /// delegates to a transport helper that holds the socket, so killing only the parent leaves the
    /// child holding it open.
    /// </para>
    /// </remarks>
    /// <param name="arguments">The git command line.</param>
    /// <param name="bound">How long to wait, or null to wait indefinitely as local calls do.</param>
    internal static (int Code, string Output, string Errors, bool TimedOut) Git(
        string arguments, TimeSpan? bound = null)
    {
        using var git = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = TheBuild.RepositoryRoot().FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("git did not start");

        var output = git.StandardOutput.ReadToEndAsync();
        var errors = git.StandardError.ReadToEndAsync();

        if (bound is { } limit && !git.WaitForExit((int)limit.TotalMilliseconds))
        {
            git.Kill(entireProcessTree: true);

            // Bounded again on purpose: the timeout path must contain no unbounded wait, or the
            // fix reintroduces the defect on the one path that exists because waiting went wrong.
            git.WaitForExit((int)limit.TotalMilliseconds);
            return (TimedOutCode, string.Empty, string.Empty, true);
        }

        git.WaitForExit();
        return (git.ExitCode, output.Result, errors.Result, false);
    }
}
