using System;
using System.Collections.Generic;
using DungeonMasterXIV.Services;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-8. Note what none of these assert: that <c>UnwindAll</c> does not rethrow. An empty
/// <c>catch</c> that runs nothing else satisfies that perfectly, and an empty catch is the shape
/// being fixed rather than the fix. Every test here names the steps that must still have run.
/// </summary>
public class TeardownSequenceTests
{
    private static TeardownSequence SequenceOf(List<string> ran, params string[] names)
    {
        var sequence = new TeardownSequence();
        foreach (var name in names)
        {
            var captured = name;
            sequence.Push(captured, () => ran.Add(captured));
        }

        return sequence;
    }

    private static void Ignore(string name, Exception exception)
    {
    }

    // Fails if: isolation is implemented by reordering, or by running steps in push order. The order
    // carries real constraints -- a frame handler must detach before what it draws is removed -- so
    // wrapping the steps must not move them.
    [Fact]
    public void StepsRunInReverseOfThePushOrder()
    {
        var ran = new List<string>();

        SequenceOf(ran, "first", "second", "third").UnwindAll(Ignore);

        Assert.Equal(new[] { "third", "second", "first" }, ran);
    }

    // Fails if: a throwing step abandons the rest of the stack. This is the bug: the socket teardown
    // sits in the middle, and the window removals are what comes after it.
    [Fact]
    public void EveryStepAfterAThrowingOneStillRuns()
    {
        var ran = new List<string>();
        var sequence = new TeardownSequence();

        sequence.Push("first", () => ran.Add("first"));
        sequence.Push("second", () => ran.Add("second"));
        sequence.Push("throws", () => throw new InvalidOperationException("socket teardown failed"));
        sequence.Push("fourth", () => ran.Add("fourth"));
        sequence.Push("fifth", () => ran.Add("fifth"));

        sequence.UnwindAll(Ignore);

        // Reverse push order, with the thrower absent and nothing after it missing.
        Assert.Equal(new[] { "fifth", "fourth", "second", "first" }, ran);
    }

    // Fails if: the failure is swallowed. A teardown that hides its own failure turns a loud
    // host-level complaint into a plugin that is quietly half unwound.
    [Fact]
    public void TheFailingStepIsReportedByNameWithItsException()
    {
        var thrown = new InvalidOperationException("socket teardown failed");
        var failures = new List<(string Name, Exception Exception)>();
        var sequence = new TeardownSequence();

        sequence.Push("window", () => { });
        sequence.Push("relay connection", () => throw thrown);

        sequence.UnwindAll((name, exception) => failures.Add((name, exception)));

        var failure = Assert.Single(failures);
        Assert.Equal("relay connection", failure.Name);
        Assert.Same(thrown, failure.Exception);
    }

    // Fails if: only the first failure is reported, or the second thrower stops the run.
    [Fact]
    public void EveryThrowingStepIsReportedAndTheSurvivorsStillRun()
    {
        var ran = new List<string>();
        var failed = new List<string>();
        var sequence = new TeardownSequence();

        sequence.Push("first", () => ran.Add("first"));
        sequence.Push("throws early", () => throw new InvalidOperationException());
        sequence.Push("middle", () => ran.Add("middle"));
        sequence.Push("throws late", () => throw new InvalidOperationException());
        sequence.Push("last", () => ran.Add("last"));

        sequence.UnwindAll((name, _) => failed.Add(name));

        Assert.Equal(new[] { "last", "middle", "first" }, ran);
        Assert.Equal(new[] { "throws late", "throws early" }, failed);
    }

    // Fails if: a thrower leaves its own step, or the ones below it, on the stack. A second unwind
    // re-running a step would undo the same registration twice on the next disable.
    [Fact]
    public void TheSequenceIsEmptiedEvenWhenAStepThrows()
    {
        var ran = new List<string>();
        var sequence = new TeardownSequence();

        sequence.Push("first", () => ran.Add("first"));
        sequence.Push("throws", () => throw new InvalidOperationException());

        sequence.UnwindAll(Ignore);
        ran.Clear();

        sequence.UnwindAll(Ignore);

        Assert.Empty(ran);
    }

    // Fails if: the reporter fires on an ordinary teardown, which would make the log line meaningless
    // by appearing every time the plugin unloads cleanly.
    [Fact]
    public void ACleanUnwindReportsNothing()
    {
        var ran = new List<string>();
        var failed = new List<string>();

        SequenceOf(ran, "first", "second").UnwindAll((name, _) => failed.Add(name));

        Assert.Equal(new[] { "second", "first" }, ran);
        Assert.Empty(failed);
    }
}
