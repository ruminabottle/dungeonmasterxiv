using System;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-123: when the size gate cannot run, the DEFAULT <c>dotnet test</c> invocation says WHY.
/// </summary>
/// <remarks>
/// <para>
/// <b>The skip fired correctly and its reason was invisible where people look.</b> <c>Skip</c> is
/// printed at <c>-v n</c> and above; the default invocation prints the test's DISPLAY NAME and
/// nothing else about a skip. Measured on a tree behind main: the reason appeared <b>0</b> times on
/// the default and <b>1</b> time under <c>-v n</c>. So a reader on the invocation everyone actually
/// runs got a bare test name, and had to already know that test's skip condition to interpret it.
/// </para>
/// <para>
/// <b>Why that matters more than an ordinary skip.</b> The gate exists so a green meaning <i>"some
/// tree was clean"</i> cannot be mistaken for one meaning <i>"the MERGED tree is clean"</i>. The skip
/// is the only thing keeping those apart — so its reason being invisible reproduces the exact
/// confusion the gate was built to end, one level up. <c>Failed 0 / Passed 279 / Skipped 1</c> reads
/// as a pass to anyone not already counting the third number.
/// </para>
/// <para>
/// <b>THIS IS A DECLARED PROXY, and the limit is the honest half.</b> These tests hold the STRING —
/// that the display name carries the reason and keeps the test's identity. They cannot show that the
/// runner PRINTS a display name on the default invocation; that is the runner's behaviour, and it was
/// established by measurement instead, before and after, both on the default form. Neither half is
/// sufficient alone: the measurement proves the channel works today, and this proves the message
/// still travels down it tomorrow.
/// </para>
/// </remarks>
public class TheSkipSaysWhyOnTheDefaultInvocationTests
{
    private const string NotContained = "origin/main is not an ancestor of HEAD";

    // THE DEFECT. Fails if the display name stops carrying the reason -- which is the whole of what
    // the default invocation shows about a skip.
    [Fact]
    public void TheDisplayNameSaysWhyTheGateDidNotRun()
    {
        var shown = ContainsMainFactAttribute.SkippedDisplayName("AnyTest", NotContained);

        Assert.Contains(NotContained, shown, StringComparison.Ordinal);
    }

    // AND IT KEEPS THE TEST'S IDENTITY. A display name carrying only the reason would tell a reader
    // why something skipped without saying WHAT -- trading one missing half for the other.
    [Fact]
    public void TheDisplayNameStillNamesTheTest()
    {
        var shown = ContainsMainFactAttribute.SkippedDisplayName("TheGateRefusesNothingOnThisTree", NotContained);

        Assert.StartsWith("TheGateRefusesNothingOnThisTree", shown, StringComparison.Ordinal);
    }

    // THE NAME IS THE COMPILER'S, NOT A LITERAL. Fails if someone hardcodes the method name back in,
    // which would survive a rename and leave a display name labelling the wrong test.
    [Fact]
    public void TheNameIsWhateverItIsGiven()
    {
        Assert.StartsWith(
            "SomeQuiteDifferentName",
            ContainsMainFactAttribute.SkippedDisplayName("SomeQuiteDifferentName", NotContained),
            StringComparison.Ordinal);
    }

    // NOT-CONTAINED IS NOT THE ONLY WAY THE CHECK ANSWERS FALSE. A git failure answers false too --
    // deliberately, so a broken git cannot be read as "contained" -- and on that arm a display name
    // asserting non-containment would state something FALSE. Fails if the cause is hardcoded back
    // in rather than repeated from the detail the check supplies.
    [Fact]
    public void AGitFailureIsNotReportedAsANonContainedBranch()
    {
        const string GitFailed = "git could not answer (exit 128): not a git repository";

        var shown = ContainsMainFactAttribute.SkippedDisplayName("AnyTest", GitFailed);

        Assert.Contains(GitFailed, shown, StringComparison.Ordinal);
        Assert.DoesNotContain("is not an ancestor", shown, StringComparison.Ordinal);
    }

    // THE BUILDER IS ACTUALLY USED, AND THE PASSING ARM IS LEFT ALONE. Everything above calls the
    // builder directly, so all of it stays green if the DisplayName assignment is simply deleted --
    // the defect restored with its own regression tests still passing. This constructs the real
    // attribute and asserts the two properties move TOGETHER.
    //
    // ON A CONTAINS-MAIN TREE -- which is what CI and the merge gate run -- this is the OTHER arm:
    // a DisplayName set unconditionally would rename a test that is running fine, so the null here
    // is an assertion, not an absence.
    //
    // THE LIMIT, STATED: on a contains-main tree this cannot catch DELETING the assignment, because
    // both properties are legitimately null. That case reddens only on a behind-main tree, where it
    // was verified by mutation rather than left to argument.
    [Fact]
    public void TheSkipAndTheDisplayNameAreSetTogether()
    {
        var gate = new ContainsMainFactAttribute("SomeTestName");

        if (gate.Skip is null)
        {
            Assert.Null(gate.DisplayName);
            return;
        }

        Assert.NotNull(gate.DisplayName);
        Assert.StartsWith("SomeTestName", gate.DisplayName, StringComparison.Ordinal);
    }

    // AND THE TWO ARMS ARE DISTINGUISHABLE. The assertions above pass individually against a name
    // that concatenated every known reason; this fails unless the one that APPLIES is the one shown.
    [Fact]
    public void DifferentReasonsProduceDifferentDisplayNames()
    {
        Assert.NotEqual(
            ContainsMainFactAttribute.SkippedDisplayName("AnyTest", NotContained),
            ContainsMainFactAttribute.SkippedDisplayName("AnyTest", "git could not answer (exit 128)"));
    }
}
