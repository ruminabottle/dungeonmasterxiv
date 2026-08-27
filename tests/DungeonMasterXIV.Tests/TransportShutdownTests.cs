using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-5. Note what none of these assert: that dispose was called. Disposal already happened before
/// the fix — that is precisely how a socket got dropped without a close frame — so "dispose ran" is
/// satisfied by the broken code and proves nothing. What is asserted is that the close is
/// <em>attempted first</em>, and that a close which never finishes still does not hold up disposal.
/// </summary>
public class TransportShutdownTests
{
    private static readonly TimeSpan ShortBound = TimeSpan.FromMilliseconds(50);

    // Fails if: disposal happens without a close being attempted, or before it. This is the bug.
    [Fact]
    public void TheCloseIsAttemptedBeforeDisposal()
    {
        var order = new List<string>();

        var failure = TransportShutdown.CloseThenDispose(
            _ => { order.Add("close"); return Task.CompletedTask; },
            () => order.Add("dispose"),
            ShortBound);

        Assert.Equal(new[] { "close", "dispose" }, order);
        Assert.Null(failure);
    }

    // Fails if: the wait is unbounded. An unbounded close against a dead or hostile peer is a plugin
    // that never finishes unloading, which is a worse failure than the one being fixed.
    [Fact]
    public void ACloseThatNeverCompletesStillDisposesAndReportsWhy()
    {
        var order = new List<string>();

        var failure = TransportShutdown.CloseThenDispose(
            _ => { order.Add("close"); return new TaskCompletionSource().Task; },
            () => order.Add("dispose"),
            ShortBound);

        Assert.Equal(new[] { "close", "dispose" }, order);
        Assert.IsType<TimeoutException>(failure);
    }

    // Fails if: a throwing close aborts the sequence and leaks the connection on this end too.
    [Fact]
    public void ACloseThatFaultsStillDisposesAndReportsTheCause()
    {
        var order = new List<string>();
        var thrown = new InvalidOperationException("socket was not open");

        var failure = TransportShutdown.CloseThenDispose(
            _ => { order.Add("close"); return Task.FromException(thrown); },
            () => order.Add("dispose"),
            ShortBound);

        Assert.Equal(new[] { "close", "dispose" }, order);
        Assert.Same(thrown, failure);
    }

    // Fails if: a close that throws synchronously, before it ever returns a task, skips disposal.
    [Fact]
    public void ACloseThatThrowsBeforeReturningATaskStillDisposes()
    {
        var order = new List<string>();
        var thrown = new ObjectDisposedException("socket");

        var failure = TransportShutdown.CloseThenDispose(
            _ => { order.Add("close"); throw thrown; },
            () => order.Add("dispose"),
            ShortBound);

        Assert.Equal(new[] { "close", "dispose" }, order);
        Assert.Same(thrown, failure);
    }

    // Fails if: the close is handed a token that never cancels. Without this the bound stops the
    // caller waiting but leaves the close itself running on with nobody listening.
    [Fact]
    public void TheCloseIsGivenATokenThatCancelsAtTheBound()
    {
        CancellationToken observed = default;

        TransportShutdown.CloseThenDispose(
            token => { observed = token; return new TaskCompletionSource().Task; },
            () => { },
            ShortBound);

        Assert.True(observed.IsCancellationRequested);
    }

    // Fails if: the failure of a clean close is reported as an error, which would make the warning
    // meaningless by firing on every ordinary disconnect.
    [Fact]
    public void ACleanCloseReportsNoFailure()
    {
        var failure = TransportShutdown.CloseThenDispose(
            _ => Task.CompletedTask,
            () => { },
            ShortBound);

        Assert.Null(failure);
    }

    [Fact]
    public void TheCloseTimeoutIsBoundedAndPositive()
    {
        // A zero or negative bound would skip the handshake entirely and reinstate the bug; an
        // unbounded one would hang shutdown. Both are the failures this type exists to prevent.
        Assert.True(TransportShutdown.CloseTimeout > TimeSpan.Zero);
        Assert.True(TransportShutdown.CloseTimeout < TimeSpan.FromSeconds(5));
    }
}
