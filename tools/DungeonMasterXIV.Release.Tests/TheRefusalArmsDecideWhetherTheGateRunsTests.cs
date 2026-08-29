using System;
using System.Collections.Generic;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// The three currency arms added by #185 refuse, and each refuses for its own reason (BUG-128).
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms decide whether the merge gate runs at all</b>, and until now each of the three
/// strings appeared exactly once in the repository — in the source that produces it. They are the
/// mechanism keeping <i>"could not validate"</i> distinct from <i>"clean"</i>, which is the entire
/// purpose of BUG-124's fix, and nothing held them.
/// </para>
/// <para>
/// <b>EVERY REFUSAL TEST ANSWERS THE ANCESTRY QUESTION WITH "YES, CONTAINED".</b> That is the whole
/// design of this file. If the fake said "not an ancestor", a <c>false</c> result would prove
/// nothing — the last arm produces <c>false</c> too, and a test asserting <c>false</c> would pass
/// with the arm under test deleted. By answering exit 0, the ONLY route to <c>false</c> is the
/// refusal arm being exercised. The assertion is therefore about which arm fired, not about the
/// return value.
/// </para>
/// <para>
/// <b>The control pins the TRUE path, which nothing else here does.</b> It does NOT rescue the three
/// refusals from vacuity — that claim was made in an earlier revision of this comment and is false:
/// a <c>Decide</c> returning <c>(false, string.Empty)</c> unconditionally reddens all four, because
/// each refusal test asserts a distinguishing substring of the detail and the empty string contains
/// none of them. Measured, not reasoned: Failed 4, Total 290.
/// </para>
/// <para>
/// What the control actually holds is the case the refusals cannot see. A <c>Decide</c> that returned
/// a <i>plausible</i> refusal for every input — including a healthy tree — satisfies all three
/// refusal tests, and <b>the gate would then never run while the suite stayed green</b>. That is
/// measured too: making the contained arm return <c>false</c> with its own unchanged detail reddens
/// this test and only this test.
/// </para>
/// <para>
/// <b>THE MASKING IS POSITIONAL, and it is a property of the chain rather than of any one arm.</b>
/// Arms 1 and 2 fall through into the stale comparison, which returns <c>false</c> for both — so
/// disabling either leaves the return value unchanged and only the detail assertion reddens. The
/// general form: <b>in an ordered chain of refusal arms, every arm above the last false-returning arm
/// can be masked by it.</b> Any arm added above the stale comparison is in that region by
/// construction and needs a which-arm assertion from the day it is written, not after a mutation
/// finds it.
/// </para>
/// <para>
/// <b>What this does NOT pin, stated rather than implied.</b> It proves the arm SELECTED for a given
/// set of git answers. It does not prove real git gives those answers — that <c>ls-remote</c> prints
/// a tab-separated sha, or that <c>--is-ancestor</c> exits 1 rather than 2 for "no". That half is
/// held by the clone-driving runs on #185 and by the gate running for real at every merge.
/// </para>
/// </remarks>
public class TheRefusalArmsDecideWhetherTheGateRunsTests
{
    private const string OriginHead = "1111111111111111111111111111111111111111";
    private const string StaleHead = "2222222222222222222222222222222222222222";

    // THE CONTROL, AND THE ONLY TEST HERE THAT PINS THE TRUE PATH. The three refusals all survive a
    // Decide that refuses everything, provided it refuses plausibly -- and a gate that never runs
    // reports a green suite. This is the test that fails in that world.
    [Fact]
    public void AllGoodAndCurrentIsContained()
    {
        var (contains, detail) = ContainsMainFactAttribute.Decide(Git());

        Assert.True(contains, $"A current cache and an ancestor origin/main must run the gate: {detail}");
        Assert.Contains("is current and an ancestor", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreachableOriginRefusesRatherThanFallingBackToTheLocalAnswer()
    {
        var (contains, detail) = ContainsMainFactAttribute.Decide(
            Git(lsRemote: (128, string.Empty, "fatal: could not read from remote repository")));

        Assert.False(
            contains,
            "origin was unreachable and the gate ran anyway. Degrading to the local answer when the "
            + "remote cannot be reached is exactly the behaviour BUG-124 removed: it reports a tree "
            + $"this check has not validated as clean. Detail was: {detail}");

        Assert.Contains("could not reach origin", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ACloneWithNoCachedOriginMainRefusesRatherThanComparingAgainstNothing()
    {
        var (contains, detail) = ContainsMainFactAttribute.Decide(
            Git(revParse: (128, string.Empty, "fatal: ambiguous argument")));

        Assert.False(
            contains,
            "There is no refs/remotes/origin/main to compare against and the gate ran anyway. "
            + $"Detail was: {detail}");

        Assert.Contains("no refs/remotes/origin/main", detail, StringComparison.Ordinal);
    }

    // THIS IS BUG-124's DEFECT ITSELF, not merely the arm that reports it. The cache is six commits
    // behind origin and merge-base says HEAD contains the CACHED ref -- which is true, and which was
    // the exact false green #185 exists to stop: contained-against-a-stale-cache read as contained.
    [Fact]
    public void AStaleCacheRefusesEvenWhenAncestryWouldSayContained()
    {
        var (contains, detail) = ContainsMainFactAttribute.Decide(
            Git(revParse: (0, StaleHead + "\n", string.Empty)));

        Assert.False(
            contains,
            "The cached origin/main is stale and the gate ran on the strength of an ancestry answer "
            + "measured against that stale cache. That is BUG-124 exactly: the gate passes against a "
            + $"tree nobody is going to merge. Detail was: {detail}");

        Assert.Contains("is STALE", detail, StringComparison.Ordinal);
        Assert.Contains(StaleHead[..7], detail, StringComparison.Ordinal);
        Assert.Contains(OriginHead[..7], detail, StringComparison.Ordinal);
    }

    /// <summary>A git that answers the three questions <c>Decide</c> asks, and nothing else.</summary>
    /// <remarks>
    /// <para>
    /// Defaults are the HEALTHY case — origin reachable, cache current, HEAD an ancestor — so each
    /// test overrides exactly the one answer its arm turns on, and the override IS the test's premise.
    /// </para>
    /// <para>
    /// <b>It throws on a command it does not recognise rather than returning a benign default.</b> A
    /// fake that shrugs at an unexpected question lets the code under test change what it asks git
    /// while every test here keeps passing — the tests would then be pinning a conversation nobody is
    /// having. An unrecognised command is a red with the command in the message.
    /// </para>
    /// </remarks>
    private static Func<string, (int Code, string Output, string Errors)> Git(
        (int Code, string Output, string Errors)? lsRemote = null,
        (int Code, string Output, string Errors)? revParse = null,
        (int Code, string Output, string Errors)? mergeBase = null)
    {
        var asked = new List<string>();

        return arguments =>
        {
            asked.Add(arguments);

            if (arguments.StartsWith("ls-remote", StringComparison.Ordinal))
            {
                return lsRemote ?? (0, $"{OriginHead}\trefs/heads/main\n", string.Empty);
            }

            if (arguments.StartsWith("rev-parse", StringComparison.Ordinal))
            {
                return revParse ?? (0, OriginHead + "\n", string.Empty);
            }

            // ANSWERED "YES, CONTAINED" BY DEFAULT, AND THAT IS THE POINT. Every refusal test above
            // leaves this alone, so the ancestry arm stands ready to return true. A false result
            // therefore cannot have come from here.
            if (arguments.StartsWith("merge-base", StringComparison.Ordinal))
            {
                return mergeBase ?? (0, string.Empty, string.Empty);
            }

            throw new InvalidOperationException(
                $"Decide asked git something this fake does not answer: '{arguments}'. Asked so far: "
                + string.Join(" | ", asked)
                + ". The fake answers ls-remote, rev-parse and merge-base; if the check now needs "
                + "another command, these tests are pinning a conversation it no longer has.");
        };
    }
}
